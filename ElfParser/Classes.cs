﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElfParser
{
    public class Module
    {
        public string Name { get; private set; }
        public List<Variable> VariableList { get; private set; }

        public Module(string name)
        {
            Name = name;
            VariableList = new List<Variable>();
        }

        public void AddVariable(Variable var)
        {
            VariableList.Add(var);
        }
    }

    public class Variable
    {
      public string Name { get; private set; }
        public string Type { get; set; }
        public int Address { get; set; }
        public int[] ArraySize { get; set; }
        public int ByteSize { get; set; }
        public List<Variable> VariableList { get; private set; }

        public Variable(string name)
        {
            Name = name;
            Type = "";
            Address = 0;
            ArraySize = new int[2] { 1, 1 };
            ByteSize = 0;
            VariableList = new List<Variable>();
        }
        
        public void AddVariable(Variable var)
        {
            VariableList.Add(var);
        }
    }

    class Typedef
    {
      public string Name { get; private set; }
      public List<Member> MemberList { get; private set; }

        public Typedef(string name)
        {
            Name = name;
            MemberList = new List<Member>();
        }

        public void AddMember(Member member)
        {
            MemberList.Add(member);
        }
    }

    class Member
    {
      public string Name { get; private set; }
      public string TypeName { get; private set; }
      public int RelAddress { get; private set; }

        public Member(string name, string typeName, int relAddress)
        {
            Name = name;
            TypeName = typeName;
            RelAddress = relAddress;
        }
    }
}
