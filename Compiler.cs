using System;
using System.Collections.Generic;
using System.IO;

namespace UserCodeLib
{
	internal class Compiler
	{
		//language data
		Dictionary<string, byte>	mInstructions	=new Dictionary<string, byte>();
		Dictionary<byte, int>		mNumOperands	=new Dictionary<byte, int>();
		Dictionary<string, byte>	mValidTypes		=new Dictionary<string, byte>();

		//file data
		List<string>	mLabels, mVars;
		List<UInt64>	mLabelAddrs, mVarAddrs;


		Screen	mScreen;

		const UInt32	ExeMagic		=0xF00CF00D;	//exe marker
		const UInt32	StartVarAddr	=8;				//first address for variables
		const byte		SrcRegister		=1;				//ex: mov reg00, reg01
		const byte		SrcPointer		=2;				//ex: mov [69], reg00
		const byte		SrcLabel		=3;				//ex: jmp label
		const byte		SrcNumber		=4;				//ex: mov 7, reg00
		const byte		SrcRegPointer	=5;				//ex: mov [reg00], reg01
		const byte		SrcVariable		=6;				//ex: mov i, reg00 Converts to SrcPointer after compile
		const byte		DstRegister		=SrcRegister << 4;
		const byte		DstPointer		=SrcPointer << 4;
		const byte		DstLabel		=SrcLabel << 4;
		const byte		DstNumber		=SrcNumber << 4;
		const byte		DstRegPointer	=SrcRegPointer << 4;
		const byte		DstVariable		=SrcVariable << 4;


		internal Compiler(Screen scr)
		{
			mScreen	=scr;

			Init();
		}


		internal bool	Compile(StreamReader src, Ram exe)
		{
			//first write an exe magic
			exe.WriteDWord(ExeMagic);

			//prescan for labels / vars
			GrabLabels(src);
			GrabVars(src);

			//skip exe dword
			exe.SetPointer(4);

			//parse lines
			for(int i=0;;i++)
			{
				string	line	=src.ReadLine();

				if(!ParseLine(line, i, exe))
				{
					return	false;
				}

				if(src.EndOfStream)
				{
					break;
				}
			}

			//the addresses of labels will be known now
			return	ReplaceLabelAddrs(exe);
		}


		bool ReplaceLabelAddrs(Ram exe)
		{
			UInt64	endExe	=exe.GetPointer();

			//buzz through compiled code and fix labels
			//skip exe number
			exe.SetPointer(4);

			for(int lineNum=0;;lineNum++)
			{
				byte	instruction	=exe.ReadByte();

				int	numArgs	=mNumOperands[instruction];
				if(numArgs == 0)
				{
					continue;
				}

				byte	args	=exe.ReadByte();
				if((args & 0xF) == SrcLabel)
				{
					//label
					int	labelIndex	=(int)ReadExeValue(exe);
					if(labelIndex < 0 || labelIndex > mLabels.Count)
					{
						mScreen.Print("Invalid label index: " + labelIndex + " at line " + lineNum);
						return	false;
					}

					//back up pointer
					UInt64	pointer	=exe.GetPointer();
					pointer	-=(UInt64)exe.GetAddressSize();
					exe.SetPointer(pointer);

					//write proper address
					WriteExeValue(exe, mLabelAddrs[labelIndex], lineNum);
				}
				else if((args & 0xF) == SrcRegister || (args & 0xF) == SrcRegPointer)
				{
					exe.ReadByte();
				}
				else
				{
					ReadExeValue(exe);
				}

				//one more argument?
				if(numArgs <= 1)
				{
					continue;
				}

				if((args & 0xF0) == DstRegister || (args & 0xF0) == DstRegPointer)
				{
					//register dst
					exe.ReadByte();
				}
				else if((args & 0xF0) == DstLabel)
				{
					//label
					int	labelIndex	=(int)ReadExeValue(exe);
					if(labelIndex < 0 || labelIndex > mLabels.Count)
					{
						mScreen.Print("Invalid label index: " + labelIndex + " at line " + lineNum);
						return	false;
					}

					//back up pointer
					UInt64	pointer	=exe.GetPointer();
					pointer	-=(UInt64)exe.GetAddressSize();
					exe.SetPointer(pointer);

					//write proper address
					WriteExeValue(exe, mLabelAddrs[labelIndex], lineNum);
				}
				else
				{
					//pointer or number
					ReadExeValue(exe);
				}

				UInt64	curPos	=exe.GetPointer();
				if(curPos >= endExe)
				{
					break;
				}
			}
			
			return	true;
		}


		bool ParseLine(string	codeLine,
					   int		lineNum,
					   Ram		exe)
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

				mLabelAddrs[labIdx]	=exe.GetPointer();

				return	true;
			}

			byte	instruction	=0;
			string	lowerTok	=toks[0].ToLowerInvariant();

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
			//The first four bits for src, last four for dst
			//0 none, 1 register, 2 ram address, 3 label address,
			//4 numerical constant, 5 address in register, 6 variable,
			//I think x86 crams this into the first byte
			byte	argIndicator	=0;

			//src
			int		tokIdx	=1;
			UInt64	srcArg	=0;
			for(;tokIdx < toks.Length;)
			{
				string	tok	=toks[tokIdx++].ToLowerInvariant();

				byte	result	=ParseArgToken(tok, ref argIndicator,
											   ref srcArg, lineNum);

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

			if(numOperands == 1)
			{
				ExeWrite(exe, lineNum, instruction, argIndicator, srcArg, 0);
				return	true;
			}

			//dst
			UInt64	dstArg	=0;
			byte	dstInd	=0;
			for(;tokIdx < toks.Length;)
			{
				string	tok	=toks[tokIdx++].ToLowerInvariant();

				byte	result	=ParseArgToken(tok, ref dstInd,
											   ref srcArg, lineNum);
				if(result == 2)
				{
					return	false;
				}
				else if(result == 1)
				{
					dstInd 			<<=4;
					argIndicator	|=dstInd;
					break;
				}
			}

			ExeWrite(exe, lineNum, instruction, argIndicator, srcArg, dstArg);

			return	true;
		}

		//return 1 if grabbed arg, 0 if didn't, 2 if error
		byte ParseArgToken(string tok, ref byte argIndicator, ref UInt64 arg, int lineNum)
		{
			tok	=tok.Trim();
			tok	=tok.Trim(',');	//rid of comma if here

			if(tok == "")
			{
				return	0;
			}

			if(tok.StartsWith("reg"))
			{
				argIndicator	=SrcRegister;
				GetRegisterArg(tok, ref arg, lineNum);
			}
			else if(tok.StartsWith("["))
			{
				string	pointerTok	=tok.Trim('[', ']');
				if(char.IsDigit(pointerTok[0]))
				{
					argIndicator	=SrcPointer;
					GetAddressArg(tok, ref arg, lineNum);
				}
				else if(pointerTok.StartsWith("reg"))
				{
					argIndicator	=SrcRegPointer;
					GetRegisterArg(pointerTok, ref arg, lineNum);
				}
				else
				{
					mScreen.Print("Syntax error on line: " + lineNum);
					return	2;
				}
			}
			else
			{
				//label or numerical constant?
				if(char.IsDigit(tok[0]))
				{
					argIndicator	=SrcNumber;
					GetNumericalArg(tok, ref arg, lineNum);
				}
				else if(mLabels.Contains(tok))
				{
					argIndicator	=SrcLabel;

					//the address might not be known yet
					//so just put the index of the label
					arg	=(UInt64)mLabels.IndexOf(tok);
				}
				else if(mVars.Contains(tok))
				{
					argIndicator	=SrcVariable;
					arg				=mVarAddrs[mVars.IndexOf(tok)];
				}
				else
				{
					//error of some sort?
					mScreen.Print("Unknown token: " + tok + " at line: " + lineNum);
					return	2;
				}					
			}
			return	1;
		}

		//prescan for labels
		void GrabLabels(StreamReader src)
		{
			mLabels		=new List<string>();
			mLabelAddrs	=new List<UInt64>();

			for(;;)
			{
				string	line	=src.ReadLine();

				string	[]toks	=line.Split(' ');

				if(toks[0].EndsWith(':'))
				{
					//label!
					string	lab	=toks[0].TrimEnd(':');

					if(!mLabels.Contains(lab))
					{
						if(IsValidVariable(lab))
						{
							mLabels.Add(lab);
							mLabelAddrs.Add(0);	//don't know yet
						}
					}
				}

				if(src.EndOfStream)
				{
					break;
				}
			}

			src.BaseStream.Seek(0, SeekOrigin.Begin);
		}

		//prescan for variables
		void GrabVars(StreamReader src)
		{
			mVars		=new List<string>();
			mVarAddrs	=new List<UInt64>();

			//skip exe magic number
			UInt64	varAddress	=StartVarAddr;

			for(;;)
			{
				string	line	=src.ReadLine();

				string	[]toks	=line.Split(' ', '\t', ',');

				string	tokLow	=toks[0].ToLowerInvariant();

				if(mValidTypes.ContainsKey(tokLow))
				{
					//Variable!
					foreach(string tok in toks)
					{
						if(tok == "")
						{
							continue;
						}

						if(!IsValidVariable(tok))
						{
							continue;
						}

						mVars.Add(tok);
						mVarAddrs.Add(varAddress);

						varAddress	+=mValidTypes[tokLow];
					}
				}

				if(src.EndOfStream)
				{
					break;
				}
			}

			src.BaseStream.Seek(0, SeekOrigin.Begin);
		}

		bool IsValidVariable(string varText)
		{
			//must begin with a letter
			if(!char.IsLetter(varText[0]))
			{
				return	false;
			}

			//make sure it isn't a type
			if(mValidTypes.ContainsKey(varText))
			{
				return	false;
			}

			//check for invalid characters
			foreach(char c in varText)
			{
				//symbols & other strange stuff under the numbers
				if(c < 48)
				{
					return	false;
				}

				//junk between numbers and letters (< > etc)
				if(c > 57 && c < 65)
				{
					return	false;
				}

				//[]\^
				if(c > 90 && c < 95)
				{
					return	false;
				}

				//`
				if(c == 96)
				{
					return	false;
				}

				//after z
				if(c > 122)
				{
					return	false;
				}
			}
			return	true;
		}


		bool GetRegisterArg(string tok, ref UInt64 srcArg, int lineNum)
		{
			//trim reg
			tok	=tok.Substring(3);

			byte	regAddr;
			if(!byte.TryParse(tok, out regAddr))
			{
				mScreen.Print("Invalid register: " + tok + " at line: " + lineNum);
				return	false;
			}

			srcArg	=regAddr;
			return	true;
		}

		bool GetNumericalArg(string tok, ref UInt64 srcArg, int lineNum)
		{
			if(!UInt64.TryParse(tok, out srcArg))
			{
				mScreen.Print("Invalid numerical constant: " + tok + " at line: " + lineNum);
				return	false;
			}
			return	true;
		}

		bool GetAddressArg(string tok, ref UInt64 srcArg, int lineNum)
		{
			//trim bracketses
			tok	=tok.TrimStart('[');
			tok	=tok.TrimEnd(']');

			if(!UInt64.TryParse(tok, out srcArg))
			{
				mScreen.Print("Invalid address: " + tok + " at line: " + lineNum);
				return	false;
			}
			return	true;
		}

		void ExeWrite(Ram exe, int lineNum, byte instruction, byte argIndicator, UInt64 src, UInt64 dst)
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

			if(!exe.WriteByte(argIndicator))
			{
				mScreen.Print("Error writing to exe at line " + lineNum);
				return;
			}

			if((argIndicator & 0xF) == SrcRegister
				|| (argIndicator & 0xF) == SrcRegPointer)
			{
				if(!exe.WriteByte((byte)src))	//register
				{
					mScreen.Print("Error writing to exe at line " + lineNum);
				}
			}
			else	//addr or label
			{
				WriteExeValue(exe, src, lineNum);
			}

			if(numOperands <= 1)
			{
				return;
			}

			if((argIndicator & 0xF0) == DstRegister
				|| (argIndicator & 0xF0) == DstRegPointer)
			{
				exe.WriteByte((byte)dst);	//register
			}
			else	//addr or label
			{
				WriteExeValue(exe, dst, lineNum);
			}
		}

		void WriteExeValue(Ram exe, UInt64 val, int lineNum)
		{
			if(exe.Is16Bit())
			{
				if(val > UInt16.MaxValue)
				{
					mScreen.Print("Warning:  Value: " + val + " too big to fit in 16 bits at line: " + lineNum);
				}

				if(!exe.WriteWord((UInt16)val))
				{
					mScreen.Print("Error writing to exe ram at line " + lineNum);
				}
			}
			else if(exe.Is32Bit())
			{
				if(val > UInt16.MaxValue)
				{
					mScreen.Print("Warning:  Value: " + val + " too big to fit in 16 bits at line: " + lineNum);
				}

				if(!exe.WriteDWord((UInt32)val))
				{
					mScreen.Print("Error writing to exe ram at line " + lineNum);
				}
			}
			else if(exe.Is64Bit())
			{
				if(!exe.WriteQWord((UInt64)val))
				{
					mScreen.Print("Error writing to exe ram at line " + lineNum);
				}
			}
		}

		UInt64	ReadExeValue(Ram exe)
		{
			if(exe.Is16Bit())
			{
				return	exe.ReadWord();
			}
			else if(exe.Is32Bit())
			{
				return	exe.ReadDWord();
			}
			else if(exe.Is64Bit())
			{
				return	exe.ReadQWord();
			}
			return	0;
		}


		void Init()
		{
//			mValidTypes.Add("byte", 1);
			mValidTypes.Add("int16", 2);
			mValidTypes.Add("uint16", 2);

			//count instructions
			byte	instIdx	=0;

			//mov src, dst
			mInstructions.Add("mov", instIdx);
			mNumOperands.Add(instIdx++, 2);

			mInstructions.Add("addrof", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//add src, dst
			mInstructions.Add("add", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//mul src, dst
			mInstructions.Add("mul", instIdx);
			mNumOperands.Add(instIdx++, 2);

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

			//xor src, dst
			mInstructions.Add("xor", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//or src, dst
			mInstructions.Add("or", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//and src, dst
			mInstructions.Add("and", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//tst x, y
			mInstructions.Add("tst", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//cmp x, y
			mInstructions.Add("cmp", instIdx);
			mNumOperands.Add(instIdx++, 2);

			//je label
			mInstructions.Add("je", instIdx);
			mNumOperands.Add(instIdx++, 1);
		}
	}
}
