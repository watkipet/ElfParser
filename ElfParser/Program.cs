using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using DWARFSharp.Dwarf;
using DWARFSharp.Utility;
using ELFSharp.ELF;

namespace ElfParser
{
    internal class Program
    {
        /// <summary>
        /// The usage string for command-line usage
        /// </summary>
        private const string Usage = "ElfParser.exe [OPTIONS] <My_elf_file.elf> [Symbol]";

        /// <summary>
        /// The response to the --help option
        /// </summary>
        private const string Help = @"NAME
    ElfParser.exe -- dump DWARF debug information from an ELF file.

SYNOPSIS
    " + Usage + @"

DESCRIPTION
    Parses an ELF file, looks for DWARF debug information, and dumps that 
    information.

    The following options are available:

    --help
        Prints this help message.

    --string-section-name=NAME
        Look for string information in NAME instead of .debug_str.
";

        static public List<string> Type = new List<string> {
            "CHAR",
            "BOOL",
            "U8",
            "U16",
            "U32",
            "U64",
            "S8",
            "S16",
            "S32",
            "S64",
            "X8",
            "X16",
            "X32",
            "X64",
            "F32",
            "*"
        };

        private static void Main(string[] args)
        {
            var elfName = "";
            var stringsSectioName = ".debug_str";

            // Handle optional arguments
            var optionals = args.Where(e => e.StartsWith("-")).ToList();
            string optional;

            if ((optionals.FirstOrDefault(e => e == "--help")) != null)
            {
                Console.WriteLine(Help);
                Environment.Exit(1);
            }

            if ((optional = optionals.FirstOrDefault(e => e.StartsWith("--string-section-name")))
                != null)
            {
                stringsSectioName = optional.Split('=').Last();
            }

            // Remove the optionals from subsequent parsing
            args = args.Where(e => !optionals.Contains(e)).ToArray();

            switch (args.Length)
            {
                case 1:
                    elfName = args[0];
                    break;
                default:
                    Console.WriteLine("Invalid number of parameters!");
                    Console.WriteLine("Usage: " + Usage);
                    Environment.Exit(1);
                    break;
            }

            // Load ELF file and extract .debug_str
            var elfFile = ELFReader.Load(elfName);
			EBitConverter.DataIsLittleEndian = elfFile.Endianess == Endianess.LittleEndian;
            var strData = elfFile.Sections.Where(s => s.Name == stringsSectioName).First().GetContents().ToList();

            // Parse abbreviations and compilation units
            var abbrevList = ExtractAbbrevList(elfFile);
            var cuList = ExtractCuList(elfFile, abbrevList);

            var moduleList = new List<Module>();

            #region Traverse module tree
            foreach (var cu in cuList)
            {
                // Add module
                var module = new Module(cu.GetName(strData));
                moduleList.Add(module);

                // Extract all variables with location attribute
                var dieList = cu.GetChildren().Where(d =>
                    d.Tag == DW_TAG.Variable && 
                    d.AttributeList.Exists(a => a.Name == DW_AT.Location));

                // Iterate through all variables
                foreach (var die in dieList)
                {
                    // Initialize variable
                    var name = die.GetName(strData);
                    var variable = new Variable(name);
                    var locAttr = die.AttributeList.Find(a => a.Name == DW_AT.Location);
                    variable.Address = EBitConverter.ToInt32(locAttr.Value.Skip(1).Take(4).ToArray(), 0);

                    // Traverse variable tree
                    TraverseVariableRecursive(cu, die, variable, strData, false);

                    // Add variable to module
                    if (variable.Type != "")
                        module.AddVariable(variable);
                }
            }
            #endregion

            #region Print module tree
            foreach (var module in moduleList)
            {
                // Do not print empty modules
                if (module.VariableList.Count > 0)
                {
                    Console.WriteLine(module.Name);

                    // Print all variables
                    foreach (var varTree in module.VariableList)
                        PrintVariableRecursive(varTree, "", 0);

                    Console.WriteLine();
                }
            }
            #endregion
        }
        // Traverse variable tree
        static void TraverseVariableRecursive(CompilationUnit cu, DebuggingInformationEntry die, Variable varTree, List<byte> strData, bool isPointer)
        {
            var dieList = DeflateDieListRecursive(cu.GetChildren());
            DebuggingInformationEntry typeDie;
            DWARFSharp.Dwarf.Attribute sizeDie;
            Variable memberVar;

            switch (die.Tag)
            {
                case DW_TAG.EnumerationType:
                    // Add size to variable
                    sizeDie = die.AttributeList.Find(a => a.Name == DW_AT.ByteSize);
                    // TODO: Should we be casting? Is sizeDie.Value really 8 bytes?
                    varTree.ByteSize = (int)EBitConverter.ToUInt64(sizeDie.Value, 0);

                    switch (varTree.ByteSize)
                    {
                        case 1:
                            varTree.Type = "U8";
                            break;
                        case 2:
                            varTree.Type = "U16";
                            break;
                        case 4:
                            varTree.Type = "U32";
                            break;
                        default:
                            throw new NotImplementedException("Unknown enum length!");
                            break;
                    }
                    break;
                case DW_TAG.BaseType:
                    // Add size to variable
                    sizeDie = die.AttributeList.Find(a => a.Name == DW_AT.ByteSize);
                    varTree.ByteSize = sizeDie.Value[0];
                    break; // Reached bottom of tree
                case DW_TAG.Typedef:
                    // Pointers aren't fetchable
                    if (!isPointer)
                    {
                        // Add typedef name to variable
                        varTree.Type = die.GetName(strData);

                        // Continue down tree
                        typeDie = dieList.Find(d => d.Id == die.GetTypeId());
                        TraverseVariableRecursive(cu, typeDie, varTree, strData, isPointer);
                    }
                    else
                    {
                        // Add typedef name to variable
                        //varTree.Type = die.GetName(strData) + "*";
                        varTree.Type = "*";
                        varTree.ByteSize = 4;
                    }
                    break;
                case DW_TAG.UnionType:
                    // Add size to variable
                    sizeDie = die.AttributeList.Find(a => a.Name == DW_AT.ByteSize);
                    varTree.ByteSize = sizeDie.Value[0];
                    
                    // Traverse only first member
                    var m = die.Children.First();

                    // Add new variable to variable tree
                    memberVar = new Variable(m.GetName(strData));
                    varTree.AddVariable(memberVar);

                    // Continue down tree
                    typeDie = dieList.Find(d => d.Id == m.GetTypeId());
                    TraverseVariableRecursive(cu, m, memberVar, strData, isPointer);
                    break;
                case DW_TAG.StructureType:
                    // Add size to variable
                    var size = new byte[8];
                    die.AttributeList.Find(a => a.Name == DW_AT.ByteSize).Value.CopyTo(size, 0);
                    // TODO: Should we be casting?
                    varTree.ByteSize = (int)EBitConverter.ToInt64(size, 0);

                    // Traverse each member
                    foreach (var member in die.Children)
                    {
                        // Add new variable to variable tree
                        memberVar = new Variable(member.GetName(strData));
                        varTree.AddVariable(memberVar);

                        // Continue down tree
                        typeDie = dieList.Find(d => d.Id == member.GetTypeId());
                        TraverseVariableRecursive(cu, member, memberVar, strData, isPointer);
                    }
                    break;
                case DW_TAG.ArrayType:
                    DWARFSharp.Dwarf.Attribute arrayAttr;
                    // TODO: Array sizes are stored in UData attributes. These are LEB128 and 
                    // assumed to hold up to 8 bytes. However, are array sizes only a max of 4 
                    // bytes?
                    var arraySize = new byte[8];

                    switch(die.Children.Count)
                    {
                        case 1:
                            arrayAttr = die.Children.ElementAt(0).AttributeList.Find(a => a.Name == DW_AT.UpperBound);

                            // Add array size to variable
                            if (arrayAttr != null)
                            {
                                arrayAttr.Value.CopyTo(arraySize, 0);
                                varTree.ArraySize[0] = (int)(EBitConverter.ToInt64(arraySize, 0) + 1);
                            }
                            break;
                        case 2:
                            arrayAttr = die.Children.ElementAt(0).AttributeList.Find(a => a.Name == DW_AT.UpperBound);

                            // Add array size to variable
                            if (arrayAttr != null)
                            {
                                varTree.ArraySize[0] = arrayAttr.Value[0] + 1;

                                arrayAttr = die.Children.ElementAt(1).AttributeList.Find(a => a.Name == DW_AT.UpperBound);

                                if (arrayAttr != null)
                                {
                                    arrayAttr.Value.CopyTo(arraySize, 0);
                                    varTree.ArraySize[1] = (int)(EBitConverter.ToInt64(arraySize, 0) + 1);
                                }
                            }
                            break;
                        default:
                            throw new NotImplementedException("Only support 1 and 2 dimensional arrays.");
                            break;
                    }

                    // Continue down tree
                    typeDie = dieList.Find(d => d.Id == die.GetTypeId());
                    TraverseVariableRecursive(cu, typeDie, varTree, strData, isPointer);
                    break;
                case DW_TAG.Member:
                    // Add relative member address
                    var locAttr = die.AttributeList.Find(a => a.Name == DW_AT.DataMemberLocation);
                    if (locAttr != null)
                    {
                        var addr = new byte[4];
                        locAttr.Value.Skip(1).ToArray().CopyTo(addr, 0);

                        // Skip first byte (0x23) and go to data
                        var idx = 1;
                        varTree.Address = (int)LEB128.ReadUnsigned(locAttr.Value.ToList(), ref idx); 
                    }

                    // Continue down tree
                    typeDie = dieList.Find(d => d.Id == die.GetTypeId());
                    TraverseVariableRecursive(cu, typeDie, varTree, strData, isPointer);
                    break;
                default: // volatile, pointer, et.c
                    // Continue down tree
                    var typeId = die.GetTypeId();
                    typeDie = dieList.Find(d => d.Id == typeId);

                    if (typeDie != null)
                    {
                        // Make sure to flag pointers
                        isPointer = die.Tag == DW_TAG.PointerType || isPointer;

                        // Don't recurse to a type that we're already inside
                        // TODO: Do this everywhere.
                        if (IdBelongsToParent(die, typeDie.Id))
                        {
                            break;
                        }

                        TraverseVariableRecursive(cu, typeDie, varTree, strData, isPointer);
                    }
                    break;
            }
        }

        static bool IdBelongsToParent(DebuggingInformationEntry die, int id)
        {
            if (die.Id == id)
            {
                return true;
            }

            if (die.Parent == null)
            {
                return false;
            }

            return IdBelongsToParent(die.Parent, id);
        }

        // Print variable tree
        static void PrintVariableRecursive(Variable varTree, string prepend, int startAddr)
        {
            // 1 dim array
            for (int i = 0; i < varTree.ArraySize[0]; i++)
            {
                // 2 dim array 
                for (int j = 0; j < varTree.ArraySize[1]; j++)
                {
                    var oldPrepend = prepend;

                    var output = "";
                    var arrayOffset = (i * varTree.ArraySize[1] * varTree.ByteSize) + (j * varTree.ByteSize);
                    var address = startAddr + varTree.Address + arrayOffset;

                    // Print variable and update prepend
                    if (varTree.ArraySize[0] > 1)
                    {
                        if (varTree.ArraySize[1] > 1)
                        {
                            output = String.Format("0x{0:X2};{1};{2}{3}[{4}][{5}]", address, varTree.Type, oldPrepend, varTree.Name, i, j);
                            oldPrepend = String.Format("{0}{1}[{2}][{3}]", oldPrepend, varTree.Name, i, j);
                        }
                        else
                        {
                            output = String.Format("0x{0:X2};{1};{2}{3}[{4}]", address, varTree.Type, oldPrepend, varTree.Name, i);
                            oldPrepend = String.Format("{0}{1}[{2}]", oldPrepend, varTree.Name, i);
                        }
                    }
                    else
                    {
                        if (varTree.ArraySize[1] > 1)
                        {
                            output = String.Format("0x{0:X2};{1};{2}{3}[0][{4}]", address, varTree.Type, oldPrepend, varTree.Name, j);
                            oldPrepend = String.Format("{0}{1}[0][{2}]", oldPrepend, varTree.Name, j);
                        }
                        else
                        {
                            output = String.Format("0x{0:X2};{1};{2}{3}", address, varTree.Type, oldPrepend, varTree.Name);
                            oldPrepend = String.Format("{0}{1}", oldPrepend, varTree.Name);
                        }
                    }

                    // Only print typedef of basic type
                    if (Type.Exists(t => t == varTree.Type))
                        Console.WriteLine(output);

                    // Continue down tree
                    foreach (var child in varTree.VariableList)
                    {
                        var newPrepend = oldPrepend + ".";
                        PrintVariableRecursive(child, newPrepend, address);
                    }
                }
            }
        }

        // Read and parse abbreviations from ELF file
        static List<Abbreviation> ExtractAbbrevList(IELF elfFile)
        {
            var abbrevList = new List<Abbreviation>();
            var abbrevData = elfFile.Sections.Where(s => s.Name == ".debug_abbrev").First().GetContents().ToList();

            var index = 0;
            while (index < abbrevData.Count)
            {
                var startIndex = index;
                Abbreviation abbrev;
                while ((abbrev = Parse.Abbreviation(abbrevData, ref index, startIndex)) != null)
                {
                    abbrevList.Add(abbrev);
                }
            }
            return abbrevList;
        }

        // Read and parse for compilation units from ELF file
        static List<CompilationUnit> ExtractCuList(IELF elfFile, List<Abbreviation> abbrevList)
        {
            var cuListFlat = new List<CompilationUnit>();
            var cuList = new List<CompilationUnit>();
            var index = 0;

            var infoData = elfFile.Sections.Where(s => s.Name == ".debug_info").First().GetContents().ToList();

            CompilationUnit cu;
            while ((cu = Parse.CU(infoData, ref index, abbrevList)) != null)
                cuListFlat.Add(cu);

            foreach (var c in cuListFlat)
            {
                index = 0;
                var dieList = InflateDieListRecursive(c.DieList, ref index);
                var inflatedCu = new CompilationUnit(c.Cuh, dieList);
                cuList.Add(inflatedCu);
            }

            return cuList;
        }
        
        // Group children to parent DIEs
        static List<DebuggingInformationEntry> InflateDieListRecursive(List<DebuggingInformationEntry> dieList, ref int index)
        {
            var output = new List<DebuggingInformationEntry>();
            while (index < dieList.Count)
            {
                var die = dieList.ElementAt(index);
                index++;
                if (die == null)
                    break;
                if (die.HasChildren == DW_CHILDREN.Yes)
                {
                    var childDieList = InflateDieListRecursive(dieList, ref index);
                    die.AddDieList(childDieList);
                    foreach (var child in childDieList)
                    {
                        child.Parent = die;
                    }
                }
                output.Add(die);
            }
            return output;
        }

        // Ungroup children to parent DIEs
        static List<DebuggingInformationEntry> DeflateDieListRecursive(List<DebuggingInformationEntry> dieList)
        {
            var output = new List<DebuggingInformationEntry>();
            for (var i = dieList.Count - 1; i >= 0; i--)
            {
                var die = dieList.ElementAt(i);
                output.Add(die);

                if (die.HasChildren == DW_CHILDREN.Yes)
                {
                    var childDieList = DeflateDieListRecursive(die.Children);
                    output.AddRange(childDieList);
                }
            }
            return output;
        }
    } 
}
