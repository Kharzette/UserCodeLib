using System;

namespace UserCodeLib
{
	internal class Screen
	{
		internal enum Modes
		{
			Text, Blocks, Sprites, ThreeD
		};

		Modes	mMode;
		Ram		mVidRam;
		int		mWidth, mHeight;


		internal Screen(Modes mode, int width, int height)
		{
			mMode	=mode;
			mWidth	=width;
			mHeight	=height;

			mVidRam	=new Ram();
		}


		internal void Print(string text)
		{
			//eventually this will write to vidram
			Console.WriteLine(text);
		}
	}
}