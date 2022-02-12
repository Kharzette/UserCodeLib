using System;
using System.Collections.Generic;
using System.IO;

namespace UserCodeLib
{
	internal partial class Compiler
	{
		//prescan for labels
		void GrabLabels(StreamReader code)
		{
			mLabels		=new List<string>();
			mLabelAddrs	=new List<UInt16>();

			for(;;)
			{
				string	line	=code.ReadLine();

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

				if(code.EndOfStream)
				{
					break;
				}
			}

			code.BaseStream.Seek(0, SeekOrigin.Begin);
		}

		//prescan for variables
		void GrabVars(StreamReader code)
		{
			mVars		=new List<string>();
			mVarAddrs	=new List<UInt16>();

			//skip exe magic number
			UInt16	varAddress	=StartVarAddr;

			for(;;)
			{
				string	line	=code.ReadLine();

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

				if(code.EndOfStream)
				{
					break;
				}
			}

			code.BaseStream.Seek(0, SeekOrigin.Begin);
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
	}
}
