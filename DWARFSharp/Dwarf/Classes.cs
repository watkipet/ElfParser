﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DWARFSharp.Utility;

namespace DWARFSharp.Dwarf
{
    public class CompilationUnit
    {
        public CompilationUnitHeader Cuh { get; private set; }
        public List<DebuggingInformationEntry> DieList { get; private set; }

        public CompilationUnit(CompilationUnitHeader cuh, List<DebuggingInformationEntry> dieList)
        {
            Cuh = cuh;
            DieList = dieList;
        }

        public string GetName(List<byte> strData)
        {
            return DieList.First().GetName(strData);
        }
        
        public List<DebuggingInformationEntry> GetChildren()
        {
            List<DebuggingInformationEntry> output;

            // Determine if inflated our not
            if (DieList.Count == 1)
                output = DieList.First().Children;
            else
                output = DieList;

            return output;
        }
    }

    public class CompilationUnitHeader
    {
        public int Id { get; private set; }
        public uint Length { get; private set; } // Byte length, not including this field
        ushort Version; // DWARF version
        public uint AbbrevOffset { get; private set; } // Offset into .debug_abbrev
        byte PtrSize; // Size in bytes of an address


        public CompilationUnitHeader(int id, uint length, ushort version, uint offset, byte size)
        {
            Id = id;
            Length = length;
            Version = version;
            AbbrevOffset = offset;
            PtrSize = size;
        }
    }

    public class DebuggingInformationEntry
    {
        public int Id { get; private set; }
        public ulong Code { get; private set; }
        public DW_TAG Tag { get; private set; }
        public DW_CHILDREN HasChildren { get; private set; }
        public List<Attribute> AttributeList { get; private set; }
        public List<DebuggingInformationEntry> Children { get; private set; }
        public DebuggingInformationEntry Parent { get; set; }

        public DebuggingInformationEntry(int id, ulong code, DW_TAG tag, DW_CHILDREN hasChildren)
        {
            Id = id;
            Code = code;
            Tag = tag;
            HasChildren = hasChildren;
            AttributeList = new List<Attribute>();
            Children = new List<DebuggingInformationEntry>();
        }

        public void AddAttribute(Attribute attribute)
        {
            AttributeList.Add(attribute);
        }

        public void AddDieList(List<DebuggingInformationEntry> dieList)
        {
            Children.AddRange(dieList);
        }

        public String GetName(List<byte> strData)
        {
            string output = null;
            var attr = AttributeList.Find(a => a.Name == DW_AT.Name);
            
            if(attr != null)
            {
                switch(attr.Form)
                {
                    case DW_FORM.String:
                        output = Encoding.ASCII.GetString(attr.Value);
                        break;
                    case DW_FORM.Strp:
                        var strp = EBitConverter.ToInt32(attr.Value, 0);
                        output = Read.StringPtr(strData, strp);
                        break;
                    default:
                        throw new NotImplementedException();
                        break;
                }
            }

            return output;
        }

        public int GetTypeId()
        {
            int output = 0;
            var typeId = AttributeList.Find(a => a.Name == DW_AT.Type);

          // TODO: Should we really be casting to int?
            if (typeId != null)
                output = (int) EBitConverter.ToInt64(typeId.Value, 0);

            return output;
        }
    }

    public class Attribute
    {
        public DW_AT Name { get; private set; }
        public DW_FORM Form { get; private set; }
        public byte[] Value { get; private set; }

        public Attribute(ulong name, ulong form)
        {
            Name = (DW_AT)name;
            Form = (DW_FORM)form;
        }

        public Attribute(DW_AT name, DW_FORM form, byte[] value)
        {
            Name = name;
            Form = form;
            Value = value;
        }
    }

    public class Abbreviation
    {
        public int Offset { get; private set; }
        public ulong Code { get; private set; }
        public DW_TAG Tag { get; private set; }
        public DW_CHILDREN HasChildren { get; private set; }
        public List<Attribute> AttributeList { get; private set; }

        public Abbreviation(int start, ulong code, DW_TAG tag, DW_CHILDREN hasChildren)
        {
            Offset = start;
            Code = code;
            Tag = tag;
            HasChildren = hasChildren;
            AttributeList = new List<Attribute>();
        }

        public void AddAttribute(Attribute attribute)
        {
            AttributeList.Add(attribute);
        }
    }
}
