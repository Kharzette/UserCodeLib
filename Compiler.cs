using System;
using System.Collections.Generic;
using System.IO;

namespace UserCodeLib
{
	internal partial class Compiler
	{
		//language data
		Dictionary<string, byte>	mInstructions	=new Dictionary<string, byte>();
		Dictionary<byte, int>		mNumOperands	=new Dictionary<byte, int>();
		Dictionary<string, byte>	mValidTypes		=new Dictionary<string, byte>();
		Dictionary<int, int>		mTypeSizes		=new Dictionary<int, int>();

		//file data
		List<string>	mLabels;
		List<UInt16>	mLabelAddrs;
		UInt64			mDataPageSize, mStackSize;

		//track local variables
		Dictionary<string, int>		mStackVarPositions	=new Dictionary<string, int>();
		Dictionary<string, byte>	mStackVarTypes		=new Dictionary<string, byte>();

		Screen	mScreen;

		const UInt32	ExeMagic			=0xF00CF00D;	//exe marker
		const UInt16	StartVarAddr		=8;				//first address for variables
		const UInt64	DefaultDataPage		=1024;			//1k default data page
		const UInt64	DefaultStackSize	=1024;			//1k default stack
		const byte		DstRegister			=1;				//ex: mov reg00, something
		const byte		DstPointer			=2;				//ex: mov [69], reg00
		const byte		DstLabel			=3;				//ex: jmp label
		const byte		DstNumber			=4;				//ex: cmp 4, 5
		const byte		DstRegPointer		=5;				//ex: mov [reg00], 69
		const byte		DstVariable			=6;				//ex: mov i, reg00
		const byte		SrcRegister			=DstRegister << 4;		//ex: mov reg00, reg01
		const byte		SrcPointer			=DstPointer << 4;		//ex: mov reg00, [69]
		const byte		SrcLabel			=DstLabel << 4;			//ex: mov reg00, label
		const byte		SrcNumber			=DstNumber << 4;		//ex: mov reg00, 7
		const byte		SrcRegPointer		=DstRegPointer << 4;	//ex: mov reg00, [reg01]
		const byte		SrcVariable			=DstVariable << 4;		//ex: mov reg00, i


		internal Compiler(Screen scr)
		{
			mScreen			=scr;
			mDataPageSize	=DefaultDataPage;
			mStackSize		=DefaultStackSize;

			Init();
		}


		internal bool	Compile(StreamReader code, RamChunk exe)
		{
			//first write an exe magic
			exe.WriteDWord(ExeMagic);

			//prescan for labels / vars
			GrabLabels(code);
			GrabVars(code);

			//skip exe dword, size qword, data size qword
			exe.SetPointer(4 + 8 + 8);

			//stack var tracking
			int			stackVarPosition	=0;	//track where stack variables point to
			List<int>	stackSizes			=new List<int>();

			//parse lines
			for(int i=0;;i++)
			{
				string	line	=code.ReadLine();

				if(!ParseLine(line, i, ref stackVarPosition, stackSizes, exe))
				{
					return	false;
				}

				if(code.EndOfStream)
				{
					break;
				}
			}

			UInt64	endExe	=exe.GetPointer();

			exe.SetPointer(4);

			//write the size of the code after the exe magic
			exe.WriteQWord(endExe);

			//write size of data page
			exe.WriteQWord(mDataPageSize);

			//write stack size
			exe.WriteQWord(mStackSize);

			//the addresses of labels will be known now
			return	ReplaceLabelAddrs(exe);
		}


		bool ReplaceLabelAddrs(RamChunk exe)
		{
			exe.SetPointer(4);

			UInt64	endExe	=exe.ReadQWord();

			//buzz through compiled code and fix labels
			//skip exe number, size qword, data size qword, and stack size qword
			exe.SetPointer(4 + 8 + 8 + 8);

			for(int lineNum=0;;lineNum++)
			{
				byte	instruction	=exe.ReadByte();

				int	numArgs	=mNumOperands[instruction];
				if(numArgs == 0)
				{
					continue;
				}

				byte	args	=exe.ReadByte();
				if((args & 0xF) == DstLabel)
				{
					//label
					int	labelIndex	=(int)exe.ReadWord();
					if(labelIndex < 0 || labelIndex > mLabels.Count)
					{
						mScreen.Print("Invalid label index: " + labelIndex + " at line " + lineNum);
						return	false;
					}

					//back up pointer
					UInt64	pointer	=exe.GetPointer();
					pointer	-=2;	//address size for 16 bit
					exe.SetPointer(pointer);

					//write proper address
					exe.WriteWord((UInt16)mLabelAddrs[labelIndex]);
				}
				else if((args & 0xF) == DstRegister || (args & 0xF) == DstRegPointer)
				{
					exe.ReadByte();
				}
				else
				{
					//ReadExeValue(exe);
					exe.ReadWord();
				}

				//one more argument?
				if(numArgs <= 1)
				{
					continue;
				}

				if((args & 0xF0) == SrcRegister || (args & 0xF0) == SrcRegPointer)
				{
					//register src
					exe.ReadByte();
				}
				else if((args & 0xF0) == SrcLabel)
				{
					//label
					int	labelIndex	=(int)exe.ReadWord();
					if(labelIndex < 0 || labelIndex > mLabels.Count)
					{
						mScreen.Print("Invalid label index: " + labelIndex + " at line " + lineNum);
						return	false;
					}

					//back up pointer
					UInt64	pointer	=exe.GetPointer();
					pointer	-=2;	//16 bit addressing
					exe.SetPointer(pointer);

					exe.WriteWord((UInt16)mLabelAddrs[labelIndex]);
				}
				else
				{
					//pointer or number
//					ReadExeValue(exe);
					exe.ReadWord();
				}

				if(numArgs == 3)
				{
					exe.ReadByte();
				}

				UInt64	curPos	=exe.GetPointer();
				if(curPos >= endExe)
				{
					break;
				}
			}

			return	true;
		}


		bool ParseLine(string		codeLine,
					   int			lineNum,
					   ref int		stackPos,
					   List<int>	stackSizes,
					   RamChunk		exe)
		{
			codeLine	=codeLine.Trim();

			if(codeLine == "")
			{
				return	true;	//blank line
			}

			string	[]toks	=codeLine.Split(' ', '\t');

			if(toks[0].StartsWith("//"))
			{
				//comment
				return	true;
			}

			if(toks[0].EndsWith(':'))
			{
				//label
				string	lab	=toks[0].TrimEnd(':');

				int	labIdx	=mLabels.IndexOf(lab);
				if(labIdx < 0 || labIdx >= mLabels.Count)
				{
					//some error
					mScreen.Print("Label: " + lab + " not found on line: " + lineNum);
					return	false;
				}

				mLabelAddrs[labIdx]	=(UInt16)exe.GetPointer();

				return	true;
			}

			byte	instruction	=0;
			string	lowerTok	=toks[0].ToLowerInvariant();

			if(lowerTok == "local")
			{
				//stack variable
				if(toks.Length < 3)
				{
					mScreen.Print("bad variable declaration on line: " + lineNum);
					return	false;
				}

				//grab type
				lowerTok	=toks[1].ToLowerInvariant();
				if(!mValidTypes.ContainsKey(lowerTok))
				{
					mScreen.Print("bad variable type on line: " + lineNum);
					return	false;
				}

				instruction	=mInstructions["push"];

				byte	typeCode	=mValidTypes[lowerTok];

				//write a push
				exe.WriteByte(instruction);
				exe.WriteByte(typeCode);

				//store variable name and stack pos and type
				mStackVarPositions.Add(toks[2], stackPos);
				mStackVarTypes.Add(toks[2], typeCode);

				//will have to pop all these when function returns
				stackPos	+=mTypeSizes[typeCode];

				return	true;
			}


			if(lowerTok.StartsWith("#pragma"))
			{
				//pragmastuff
				return	ParsePragma(toks);
			}

			//see if this is a variable declaration line
			if(mValidTypes.ContainsKey(lowerTok))
			{
				return	true;	//skip
			}

			if(!mInstructions.ContainsKey(lowerTok))
			{
				mScreen.Print("Invalid instruction: " + lowerTok + " on line: " + lineNum);
				return	false;	//syntax error or something
			}

			instruction	=mInstructions[lowerTok];

			int	numOperands	=mNumOperands[instruction];

			if(numOperands == 0)
			{
				return	exe.WriteByte(instruction);
			}

			//the second byte in a line's exe stream is an indicator
			//of where the arguments are:
			//The first four bits for dst, last four for src
			//0 none, 1 register, 2 ram address, 3 label address,
			//4 numerical constant, 5 address in register, 6 variable,
			//I think x86 crams this into the first byte
			byte	[]argInd		=new byte[3];
			int		tokIdx			=1;
			UInt16	[]args			=new UInt16[3];

			for(int i=0;i < numOperands;i++)
			{
				for(;tokIdx < toks.Length;)
				{
					string	tok	=toks[tokIdx++].ToLowerInvariant();

					byte	result	=ParseArgToken(tok, ref argInd[i],
												ref args[i], lineNum);

					if(result == 2)
					{
						return	false;
					}
					else if(result == 1)
					{
						break;
					}
				}

				if(tokIdx > toks.Length)
				{
					mScreen.Print("Not enough arguments for instruction: " + lowerTok + " at line: " + lineNum);
					return	false;
				}
			}

			ExeWrite(exe, lineNum, instruction, argInd, args);

			return	true;
		}


		void ExeWrite(RamChunk exe, int lineNum, byte instruction,
						byte []argInds, UInt16 []args)
		{
			int	numOperands	=mNumOperands[instruction];

			if(!exe.WriteByte(instruction))
			{
				mScreen.Print("Error writing to exe at line " + lineNum);
				return;
			}

			if(numOperands == 0)
			{
				return;
			}

			byte	argIndicator	=(byte)(argInds[0] | (argInds[1] << 4));
			if(!exe.WriteByte(argIndicator))
			{
				mScreen.Print("Error writing to exe at line " + lineNum);
				return;
			}

			if((argIndicator & 0xF) == DstRegister
				|| (argIndicator & 0xF) == DstRegPointer)
			{
				if(!exe.WriteByte((byte)args[0]))	//register
				{
					mScreen.Print("Error writing to exe at line " + lineNum);
				}
			}
			else	//addr or label
			{
				exe.WriteWord(args[0]);
			}

			if(numOperands <= 1)
			{
				return;
			}

			if((argIndicator & 0xF0) == SrcRegister
				|| (argIndicator & 0xF0) == SrcRegPointer)
			{
				exe.WriteByte((byte)args[1]);	//register
			}
			else	//addr or label
			{
				//WriteExeValue(exe, dst, lineNum);
				exe.WriteWord(args[1]);
			}

			if(numOperands == 3)
			{
				exe.WriteByte((byte)args[2]);
			}
		}


		void Init()
		{
			mValidTypes.Add("byte", 1);
			mValidTypes.Add("int16", 2);	//signed and unsigned differences?
			mValidTypes.Add("uint16", 2);

			mTypeSizes.Add(1, 1);
			mTypeSizes.Add(2, 2);

			//count instructions
			byte	instIdx	=0;

			//mov dst, src
			mInstructions.Add("mov", instIdx);
			mNumOperands.Add(instIdx++, 2);

			mInstructions.Add("addrof", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//add dst, src
			mInstructions.Add("add", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//mul dst, src
			mInstructions.Add("mul", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//integer mul dst, src
			mInstructions.Add("imul", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//div dst, src
			mInstructions.Add("div", instIdx);
			mNumOperands.Add(instIdx++, 3);

			//integer div dst, src
			mInstructions.Add("idiv", instIdx);
			mNumOperands.Add(instIdx++, 3);

			//inc dst
			mInstructions.Add("inc", instIdx);
			mNumOperands.Add(instIdx++, 1);

			//dec dst
			mInstructions.Add("dec", instIdx);
			mNumOperands.Add(instIdx++, 1);

			//neg dst
			mInstructions.Add("neg", instIdx);
			mNumOperands.Add(instIdx++, 1);

			//not dst
			mInstructions.Add("not", instIdx);
			mNumOperands.Add(instIdx++, 1);

			//xor dst, src
			mInstructions.Add("xor", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//or dst, src
			mInstructions.Add("or", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//and dst, src
			mInstructions.Add("and", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//tst x, y
			mInstructions.Add("tst", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//cmp x, y
			mInstructions.Add("cmp", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//jmp jump label
			mInstructions.Add("jmp", instIdx);			
			mNumOperands.Add(instIdx++, 1);

			//je jump equal label
			mInstructions.Add("je", instIdx);
			mNumOperands.Add(instIdx++, 1);

			//jne jump not equal label
			mInstructions.Add("jne", instIdx);
			mNumOperands.Add(instIdx++, 1);

			//jg jump greater label
			mInstructions.Add("jg", instIdx);			
			mNumOperands.Add(instIdx++, 1);

			//jge jump greater or equal label
			mInstructions.Add("jge", instIdx);			
			mNumOperands.Add(instIdx++, 1);

			//jl jump less label
			mInstructions.Add("jl", instIdx);			
			mNumOperands.Add(instIdx++, 1);

			//jle jump less or equal label
			mInstructions.Add("jle", instIdx);			
			mNumOperands.Add(instIdx++, 1);

			//jump zero
			mInstructions.Add("jz", instIdx);
			mNumOperands.Add(instIdx++, 1);

			//jump not zero
			mInstructions.Add("jnz", instIdx);
			mNumOperands.Add(instIdx++, 1);

			//conditional equal mov
			mInstructions.Add("cmove", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//conditional not equal mov
			mInstructions.Add("cmovne", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//conditional greater mov
			mInstructions.Add("cmovg", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//conditional greater or equal mov
			mInstructions.Add("cmovge", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//conditional less mov
			mInstructions.Add("cmovl", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//conditional less or equal mov
			mInstructions.Add("cmovle", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//push onto stack, might be hidden
			mInstructions.Add("push", instIdx);
			mNumOperands.Add(instIdx++, 1);
		}
	}
}
