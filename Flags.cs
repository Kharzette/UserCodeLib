using System;

namespace UserCodeLib
{
	internal class Flags
	{
		bool	bCarry;
		bool	bZero;
		bool	bSign;


		internal bool GetCarry()
		{
			return	bCarry;
		}

		internal bool GetZero()
		{
			return	bZero;
		}

		internal bool GetSign()
		{
			return	bSign;
		}


		internal void SetCarry(bool bVal)
		{
			bCarry	=bVal;
		}

		internal void SetZero(bool bVal)
		{
			bZero	=bVal;
		}

		internal void SetSign(bool bVal)
		{
			bSign	=bVal;
		}
	}
}