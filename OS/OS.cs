using System;


namespace UserCodeLib
{
	internal class OS
	{
		Ram	mRam;	//os mem page for biosy code

		AddressSpace	mSpace;	//for handing out pages

		byte	mAddrBits;	//0 for 16, 1 for 32, 2 for 64


		internal OS(byte bits)
		{
			mAddrBits	=bits;

			mSpace	=new AddressSpace(bits);

			Alloc(8192, AddressSpace.SpaceTypes.Code,
					"OS Biosy Functions", out mRam);
		}


		internal bool Alloc(UInt16 size, AddressSpace.SpaceTypes kind,
							string desc, out Ram mem)
		{
			mem	=new Ram();

			UInt16	page;
			if(!mSpace.MakePage(kind, desc, out page))
			{
				return	false;
			}

			mem.Init(size, mAddrBits, page);
			return	true;
		}


		internal void Print(string text)
		{
			Console.WriteLine(text);
		}
	}
}