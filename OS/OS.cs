using System;


namespace UserCodeLib
{
	internal class OS
	{
		Ram	mRam;	//machine memory controller

		byte	mAddrBits;	//0 for 16, 1 for 32, 2 for 64

		UInt64		mBiosModule;	//ram module that bios lives on
		RamChunk	mBiosChunk;		//ram chunk that bios lives on


		internal OS(byte bits, Ram ram, UInt64 moduleID)
		{
			mAddrBits	=bits;
			mRam		=ram;
			mBiosModule	=moduleID;

			//alloc space for bios code
			Alloc(8192, out mBiosChunk);
		}


		//just use same module as the bios lives on
		internal bool Alloc(UInt64 size, out RamChunk chonk)
		{
			return	Alloc(size, mBiosModule, out chonk);
		}


		internal bool Alloc(UInt64 size, UInt64 moduleID, out RamChunk chonk)
		{
			RamModule	rm	=mRam.GetModule(moduleID);			
			
			UInt16	page;
			if(rm.CreateChunk(size, out page))
			{
				chonk	=rm.GetChunk(page);
				return	true;
			}
			chonk	=null;
			return	false;
		}


		internal void Print(string text)
		{
			Console.WriteLine(text);
		}
	}
}