using System;
using System.Collections.Generic;
using System.IO;

namespace UserCodeLib
{
	//module to try to cram multiple devices and such
	//into user code accessable addresses
	internal class AddressSpace
	{
		internal enum SpaceTypes
		{
			Device, Code, Data
		};

		class Page
		{
			internal string		Desc;
			internal SpaceTypes	Type;
		}

		byte	mAddrBits;	//0 for 16, 1 for 32, 2 for 64
		UInt16	mLastIndex;	//last used page index

		Dictionary<UInt16, Page>	mPages	=new Dictionary<UInt16, Page>();


		internal AddressSpace(byte addrBits)
		{
			mAddrBits	=addrBits;
		}


		internal bool	MakePage(SpaceTypes kind, string desc, out UInt16 page)
		{
			Page	p	=new Page();

			p.Desc	=desc;
			p.Type	=kind;

			mPages.Add(++mLastIndex, p);

			page	=mLastIndex;

			return	true;
		}
	}
}