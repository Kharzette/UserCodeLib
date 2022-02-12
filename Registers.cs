using System;

namespace UserCodeLib
{
	internal class Registers
	{
		UInt16	[]m16Regs;
		UInt32	[]m32Regs;
		UInt64	[]m64Regs;


		const int	MaxRegs64	=32;
		const int	MaxRegs32	=24;
		const int	MaxRegs16	=12;


		internal void Init(int num16, int num32, int num64)
		{
			num16	=Math.Clamp(num16, 0, MaxRegs16);
			num32	=Math.Clamp(num32, 0, MaxRegs32);
			num64	=Math.Clamp(num64, 0, MaxRegs64);

			m16Regs	=new UInt16[num16];
			m32Regs	=new UInt32[num32];
			m64Regs	=new UInt64[num64];
		}


		internal UInt16	Get16(int index)
		{
			if(m16Regs == null || index < 0 || index >= m16Regs.Length)
			{
				return	0xFFFF;
			}
			return	m16Regs[index];
		}

		internal UInt32	Get32(int index)
		{
			if(m32Regs == null || index < 0 || index >= m32Regs.Length)
			{
				return	0xFFFFFFFF;
			}
			return	m32Regs[index];
		}

		internal UInt64	Get64(int index)
		{
			if(m64Regs == null || index < 0 || index >= m64Regs.Length)
			{
				return	0xFFFFFFFFFFFFFFFF;
			}
			return	m64Regs[index];
		}


		internal void Set16(int index, UInt16 value)
		{
			if(m16Regs == null || index < 0 || index >= m16Regs.Length)
			{
				return;
			}
			m16Regs[index]	=value;
		}


		internal void Set32(int index, UInt32 value)
		{
			if(m32Regs == null || index < 0 || index >= m32Regs.Length)
			{
				return;
			}
			m32Regs[index]	=value;
		}


		internal void Set64(int index, UInt64 value)
		{
			if(m64Regs == null || index < 0 || index >= m64Regs.Length)
			{
				return;
			}
			m64Regs[index]	=value;
		}
	}
}