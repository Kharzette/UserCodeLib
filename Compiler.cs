using System;
using System.Collections.Generic;
using System.IO;

namespace UserCodeLib
{
	internal class Compiler
	{
		Dictionary<string, byte>	mInstructions	=new Dictionary<string, byte>();
		Dictionary<byte, int>		mNumOperands	=new Dictionary<byte, int>();

		Screen	mScreen;

		const UInt32	ExeMagic	=0xF00CF00D;	//exe marker
		const byte		SrcRegister	=0x1;
		const byte		SrcAddress	=0x2;
		const byte		SrcLabel	=0x4;
		const byte		SrcNumber	=0x8;
		const byte		DstRegister	=0x1 << 4;
		const byte		DstAddress	=0x2 << 4;
		const byte		DstLabel	=0x4 << 4;
		const byte		DstNumber	=0x8 << 4;


		internal Compiler(Screen scr)
		{
			mScreen	=scr;

			Init();
		}


		internal bool	Compile(StreamReader src, Ram exe)
		{
			//first write an exe magic
			exe.WriteDWord(ExeMagic);

			//label tracking
			List<string>	labels		=new List<string>();
			List<UInt64>	labelAddrs	=new List<UInt64>();

			//prescan for labels
			for(;;)
			{
				string	line	=src.ReadLine();

				string	[]toks	=line.Split(' ');

				if(toks[0].EndsWith(':'))
				{
					//label!
					string	lab	=toks[0].TrimEnd(':');
					labels.Add(lab);
					labelAddrs.Add(0);	//don't know yet
				}

				if(src.EndOfStream)
				{
					break;
				}
			}

			src.BaseStream.Seek(0, SeekOrigin.Begin);
			for(int i=0;;i++)
			{
				string	line	=src.ReadLine();

				if(!ParseLine(line, i, labels, labelAddrs, exe))
				{
					return	false;
				}

				if(src.EndOfStream)
				{
					break;
				}
			}

			UInt64	endExe	=exe.GetPointer();

			//buzz through compiled code and fix labels
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
				if(IsArgFlagSet(args, SrcLabel))
				{
					//label
					int	labelIndex	=(int)ReadExeValue(exe);
					if(labelIndex < 0 || labelIndex > labels.Count)
					{
						mScreen.Print("Invalid label index: " + labelIndex + " at line " + lineNum);
						return	false;
					}

					//back up pointer
					UInt64	pointer	=exe.GetPointer();
					pointer	-=(UInt64)exe.GetAddressSize();
					exe.SetPointer(pointer);

					//write proper address
					WriteExeValue(exe, labelAddrs[labelIndex], lineNum);
				}
				else if(IsArgFlagSet(args, SrcRegister))
				{
					exe.ReadByte();
				}
				else if(IsArgFlagSet(args, SrcAddress))
				{
					ReadExeValue(exe);
				}
				else if(IsArgFlagSet(args, SrcNumber))
				{
					ReadExeValue(exe);
				}

				//one more argument?
				if(numArgs <= 1)
				{
					continue;
				}

				if(IsArgFlagSet(args, DstRegister))
				{
					//register dst
					exe.ReadByte();
				}
				else if(IsArgFlagSet(args, DstLabel))
				{
					//label
					int	labelIndex	=(int)ReadExeValue(exe);
					if(labelIndex < 0 || labelIndex > labels.Count)
					{
						mScreen.Print("Invalid label index: " + labelIndex + " at line " + lineNum);
						return	false;
					}

					//back up pointer
					UInt64	pointer	=exe.GetPointer();
					pointer	-=(UInt64)exe.GetAddressSize();
					exe.SetPointer(pointer);

					//write proper address
					WriteExeValue(exe, labelAddrs[labelIndex], lineNum);
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

		bool ParseLine(string		codeLine,
					   int			lineNum,
					   List<string>	labels,
					   List<UInt64>	labelAddrs,
					   Ram			exe)
		{
			codeLine	=codeLine.Trim();

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

				int	labIdx	=labels.IndexOf(lab);
				if(labIdx < 0 || labIdx >= labels.Count)
				{
					//some error
					mScreen.Print("Label: " + lab + " not found on line: " + lineNum);
					return	false;
				}

				labelAddrs[labIdx]	=exe.GetPointer();

				return	true;
			}

			byte	instruction	=0;
			string	lowerTok	=toks[0].ToLowerInvariant();
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
			//0 none, 1 register, 2 ram address, 4 label address, 8 numerical constant
			//I think x86 crams this into the first byte
			byte	argIndicator	=0;

			//src
			int		tokIdx	=1;
			UInt64	srcArg	=0;
			for(;;)
			{
				string	tok	=toks[tokIdx++].ToLowerInvariant();

				tok	=tok.Trim();
				tok	=tok.TrimEnd(',');	//rid of comma if here

				if(tok == "")
				{
					continue;
				}

				if(tok.StartsWith("reg"))
				{
					SetArgFlag(ref argIndicator, SrcRegister);
					GetRegisterArg(tok, ref srcArg, lineNum);
				}
				else if(tok.StartsWith("["))
				{
					SetArgFlag(ref argIndicator, SrcAddress);
					GetAddressArg(tok, ref srcArg, lineNum);
				}
				else
				{
					//label or numerical constant?
					if(char.IsDigit(tok[0]))
					{
						SetArgFlag(ref argIndicator, SrcNumber);
						GetNumericalArg(tok, ref srcArg, lineNum);
					}
					else if(labels.Contains(tok))
					{
						SetArgFlag(ref argIndicator, SrcLabel);

						//the address might not be known yet
						//so just put the index of the label
						srcArg	=(UInt64)labels.IndexOf(tok);
					}
					else
					{
						//error of some sort?
						mScreen.Print("Unknown token: " + tok + " at line: " + lineNum);
						return	false;
					}					
				}
				break;
			}

			if(numOperands == 1)
			{
				ExeWrite(exe, lineNum, instruction, argIndicator, srcArg, 0);
				return	true;
			}

			//dst
			UInt64	dstArg	=0;
			{
				string	tok	=toks[tokIdx++].ToLowerInvariant();

				tok	=tok.Trim();
				tok	=tok.TrimStart(',');	//rid of comma if here

				if(tok.StartsWith("reg"))
				{
					SetArgFlag(ref argIndicator, DstRegister);
					GetRegisterArg(tok, ref dstArg, lineNum);
				}
				else if(tok.StartsWith("["))
				{
					SetArgFlag(ref argIndicator, DstAddress);
					GetAddressArg(tok, ref dstArg, lineNum);
				}
				else
				{
					if(char.IsDigit(tok[0]))
					{
						SetArgFlag(ref argIndicator, DstNumber);
						GetNumericalArg(tok, ref dstArg, lineNum);
					}
					else
					{
						mScreen.Print("Unknown token: " + tok + " at line: " + lineNum);
						return	false;
					}
				}
			}

			ExeWrite(exe, lineNum, instruction, argIndicator, srcArg, dstArg);

			return	true;
		}


		void SetArgFlag(ref byte arg, byte flag)
		{
			arg	|=flag;			
		}

		bool IsArgFlagSet(byte arg, byte flag)
		{
			return	((arg & flag) != 0);
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

			if(IsArgFlagSet(argIndicator, SrcRegister))
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

			if(IsArgFlagSet(argIndicator, DstRegister))
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
			//mov src, dst
			mInstructions.Add("mov", 0x00);
			mNumOperands.Add(0x00, 2);

			//add src, dst
			mInstructions.Add("add", 0x01);
			mNumOperands.Add(0x01, 2);

			//mul src, dst
			mInstructions.Add("mul", 0x02);
			mNumOperands.Add(0x02, 2);

			//inc dst
			mInstructions.Add("inc", 0x03);
			mNumOperands.Add(0x03, 1);

			//dec dst
			mInstructions.Add("dec", 0x04);
			mNumOperands.Add(0x04, 1);

			//neg dst
			mInstructions.Add("neg", 0x05);
			mNumOperands.Add(0x05, 1);

			//not dst
			mInstructions.Add("not", 0x06);
			mNumOperands.Add(0x06, 1);

			//xor src, dst
			mInstructions.Add("xor", 0x07);
			mNumOperands.Add(0x07, 2);

			//or src, dst
			mInstructions.Add("or", 0x08);
			mNumOperands.Add(0x08, 2);

			//and src, dst
			mInstructions.Add("and", 0x09);
			mNumOperands.Add(0x09, 2);

			//tst x, y
			mInstructions.Add("tst", 0x0A);
			mNumOperands.Add(0x0A, 2);

			//cmp x, y
			mInstructions.Add("cmp", 0x0B);
			mNumOperands.Add(0x0B, 2);

			//je label
			mInstructions.Add("je", 0x0C);
			mNumOperands.Add(0x0C, 1);
		}
	}
}
