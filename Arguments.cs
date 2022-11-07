using System;
using System.Collections.Generic;
using System.IO;

namespace UserCodeLib
{
	internal partial class Compiler
	{
		//return 1 if grabbed arg, 0 if didn't, 2 if error
		byte ParseArgToken(string tok, ref byte argIndicator, ref UInt16 arg, int lineNum)
		{
			tok	=tok.Trim();
			tok	=tok.Trim(',');	//rid of comma if here

			if(tok == "")
			{
				return	0;
			}

			if(tok.StartsWith("reg"))
			{
				argIndicator	=DstRegister;
				GetRegisterArg(tok, ref arg, lineNum);
			}
			else if(tok.StartsWith("["))
			{
				string	pointerTok	=tok.Trim('[', ']');
				if(char.IsDigit(pointerTok[0]))
				{
					argIndicator	=DstPointer;
					GetAddressArg(tok, ref arg, lineNum);
				}
				else if(pointerTok.StartsWith("reg"))
				{
					argIndicator	=DstRegPointer;
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
					argIndicator	=DstNumber;
					GetNumericalArg(tok, ref arg, lineNum);
				}
				else if(mLabels.Contains(tok))
				{
					argIndicator	=DstLabel;

					//the address might not be known yet
					//so just put the index of the label
					arg	=(UInt16)mLabels.IndexOf(tok);
				}
				else if(mVars.Contains(tok))
				{
					argIndicator	=DstVariable;
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


		bool	ParsePragma(string	[]toks)
		{
			//get pragma arg
			string	arg		="";
			UInt64	numArg	=0;
			for(int i=1;i < toks.Length;i++)
			{
				if(toks[i] == "")
				{
					continue;
				}

				string	tok	=toks[i].ToLowerInvariant();

				if(char.IsNumber(tok[0]))
				{
					UInt64.TryParse(tok, out numArg);
					break;
				}
				else
				{
					arg	=tok;
				}
			}

			if(arg == "datapagesize")
			{
				mDataPageSize	=numArg;
			}
			else if(arg == "stacksize")
			{
				mStackSize	=numArg;
			}

			return	true;
		}


		bool GetRegisterArg(string tok, ref UInt16 arg, int lineNum)
		{
			//trim reg
			tok	=tok.Substring(3);

			byte	regAddr;
			if(!byte.TryParse(tok, out regAddr))
			{
				mScreen.Print("Invalid register: " + tok + " at line: " + lineNum);
				return	false;
			}

			arg	=regAddr;
			return	true;
		}

		bool GetNumericalArg(string tok, ref UInt16 arg, int lineNum)
		{
			if(!UInt16.TryParse(tok, out arg))
			{
				mScreen.Print("Invalid numerical constant: " + tok + " at line: " + lineNum);
				return	false;
			}
			return	true;
		}

		bool GetAddressArg(string tok, ref UInt16 srcArg, int lineNum)
		{
			//trim bracketses
			tok	=tok.TrimStart('[');
			tok	=tok.TrimEnd(']');

			if(!UInt16.TryParse(tok, out srcArg))
			{
				mScreen.Print("Invalid address: " + tok + " at line: " + lineNum);
				return	false;
			}
			return	true;
		}
	}
}