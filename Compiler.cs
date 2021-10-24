using System;
using System.Collections.Generic;
using System.IO;

namespace UserCodeLib
{
	internal class Compiler
	{
		Dictionary<string, byte>	mInstructions	=new Dictionary<string, byte>();
		Dictionary<byte, int>		mNumOperands	=new Dictionary<byte, int>();

		const UInt32	ExeMagic	=0xF00CF00D;	//exe marker


		internal Compiler()
		{
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
			for(;;)
			{
				string	line	=src.ReadLine();

				if(!ParseLine(line, labels, labelAddrs, exe))
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

			for(;;)
			{
				byte	instruction	=exe.ReadByte();

				byte	args	=exe.ReadByte();
				if((args & 0x4) != 0)
				{
					//label
					int	labelIndex	=0;
					if(exe.Is16Bit())
					{
						labelIndex	=exe.ReadWord();
					}
					else if(exe.Is32Bit())
					{
						labelIndex	=(int)exe.ReadDWord();
					}
					else if(exe.Is64Bit())
					{
						labelIndex	=(int)exe.ReadQWord();
					}

					if(labelIndex < 0 || labelIndex > labels.Count)
					{
						//some kind of error
						return	false;
					}

					//back up pointer
					UInt64	pointer	=exe.GetPointer();

					pointer	-=8;
					exe.SetPointer(pointer);

					//write proper address
					if(exe.Is16Bit())
					{
						exe.WriteWord((UInt16)labelAddrs[labelIndex]);
					}
					else if(exe.Is32Bit())
					{
						exe.WriteDWord((UInt32)labelAddrs[labelIndex]);
					}
					else if(exe.Is64Bit())
					{
						exe.WriteQWord(labelAddrs[labelIndex]);
					}
				}
				else if((args & 0x1) != 0)
				{
					//register
					exe.ReadByte();
				}
				else if((args & 0x2) != 0)
				{
					if(exe.Is16Bit())
					{
						exe.ReadWord();
					}
					else if(exe.Is32Bit())
					{
						exe.ReadDWord();
					}
					else if(exe.Is64Bit())
					{
						exe.ReadQWord();
					}
				}

				//one more argument?
				if((args & 0xF0) == 0)
				{
					continue;
				}

				if((args & (0x1 << 4)) != 0)
				{
					//register dst
					exe.ReadByte();
				}
				else
				{
					//pointer or label
					if(exe.Is16Bit())
					{
						exe.ReadWord();
					}
					else if(exe.Is32Bit())
					{
						exe.ReadDWord();
					}
					else if(exe.Is64Bit())
					{
						exe.ReadQWord();
					}
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
					return	false;
				}

				labelAddrs[labIdx]	=exe.GetPointer();

				return	true;
			}

			byte	instruction	=0;
			string	lowerTok	=toks[0].ToLowerInvariant();
			if(!mInstructions.ContainsKey(lowerTok))
			{
				return	false;	//syntax error or something
			}

			instruction	=mInstructions[lowerTok];

			int	numOperands	=mNumOperands[instruction];

			if(numOperands == 0)
			{
				return	exe.WriteByte(instruction);
			}

			//the next byte in the exe stream is an indicator
			//of where the arguments are
			//The first four bytes for src, last four for dst
			//0 none, 1 register, 2 ram address, 4 label address
			byte	argIndicator	=0;

			//src
			int		tokIdx	=1;
			UInt64	srcAddr	=0;
			for(;tokIdx < toks.Length;)
			{
				string	tok	=toks[tokIdx++].ToLowerInvariant();

				tok	=tok.Trim();
				tok	=tok.TrimEnd(',');	//rid of comma if here

				if(tok.StartsWith("reg"))
				{
					//register!
					argIndicator	|=0x1;

					//trim reg
					tok	=tok.Substring(3);

					byte	regAddr;
					if(!byte.TryParse(tok, out regAddr))
					{
						return	false;
					}
					srcAddr	=regAddr;
					break;
				}
				else if(tok.StartsWith("["))
				{
					//pointer!
					argIndicator	|=0x2;

					//trim bracketses
					tok	=tok.TrimStart('[');
					tok	=tok.TrimEnd(']');

					if(!UInt64.TryParse(tok, out srcAddr))
					{
						return	false;
					}
					break;
				}
				else
				{
					//label?
					if(labels.Contains(tok))
					{
						argIndicator	|=0x4;

						//the address might not be known yet
						//so just put the index of the label
						srcAddr	=(UInt64)labels.IndexOf(tok);
					}
					else
					{
						//error of some sort?
						return	false;
					}					
				}
			}

			if(numOperands == 1)
			{
				bool	ret	=exe.WriteByte(instruction);

				ret	=exe.WriteByte(argIndicator);

				if((argIndicator & 0x1) != 0)
				{
					exe.WriteByte((byte)srcAddr);	//register
				}
				else	//addr or label
				{
					if(exe.Is16Bit())
					{
						exe.WriteWord((UInt16)srcAddr);
					}
					else if(exe.Is32Bit())
					{
						exe.WriteDWord((UInt32)srcAddr);
					}
					else if(exe.Is64Bit())
					{
						exe.WriteQWord((UInt64)srcAddr);
					}
				}
				return	ret;
			}

			//dst
			UInt64	dstAddr	=0;
			for(;tokIdx < toks.Length;)
			{
				string	tok	=toks[tokIdx++].ToLowerInvariant();

				tok	=tok.Trim();
				tok	=tok.TrimStart(',');	//rid of comma if here

				if(tok.StartsWith("reg"))
				{
					//register!
					argIndicator	|=(0x1 << 4);

					//trim reg
					tok	=tok.Substring(3);

					byte	regAddr;
					if(!byte.TryParse(tok, out regAddr))
					{
						return	false;
					}
					dstAddr	=regAddr;
					break;
				}
				else if(tok.StartsWith("["))
				{
					//pointer!
					argIndicator	|=(0x2 << 4);

					//trim bracketses
					tok	=tok.TrimStart('[');
					tok	=tok.TrimEnd(']');

					if(!UInt64.TryParse(tok, out dstAddr))
					{
						return	false;
					}
					break;
				}
				else
				{
					//some kind of wacky error?
					return	false;
				}
			}

			exe.WriteByte(instruction);
			exe.WriteByte(argIndicator);

			//write src arg
			if((argIndicator & 0x1) != 0)
			{
				exe.WriteByte((byte)srcAddr);	//register
			}
			else	//addr or label
			{
				if(exe.Is16Bit())
				{
					exe.WriteWord((UInt16)srcAddr);
				}
				else if(exe.Is32Bit())
				{
					exe.WriteDWord((UInt32)srcAddr);
				}
				else if(exe.Is64Bit())
				{
					exe.WriteQWord((UInt64)srcAddr);
				}
			}

			//write dst arg
			if((argIndicator & (0x1 << 4)) != 0)
			{
				exe.WriteByte((byte)dstAddr);	//register
			}
			else	//addr or label
			{
				if(exe.Is16Bit())
				{
					exe.WriteWord((UInt16)dstAddr);
				}
				else if(exe.Is32Bit())
				{
					exe.WriteDWord((UInt32)dstAddr);
				}
				else if(exe.Is64Bit())
				{
					exe.WriteQWord((UInt64)dstAddr);
				}
			}
			return	true;
		}

		static UInt32 IndexOf(Dictionary<string, UInt64> labels, string tok)
		{
			UInt32	idx	=0;
			foreach(KeyValuePair<string, UInt64> leb in labels)
			{
				if(leb.Key == tok)
				{
					return	idx;
				}
				idx++;
			}
			return	0xFFFFFFFF;
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
