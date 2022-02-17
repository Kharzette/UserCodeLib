//Just some random test code thrown together to see what the
//disassembly looks like or to test something
#include	<stdio.h>
#include	<stdlib.h>
#include	<time.h>


int	CountDigitses(int numbarrrr)
{
	int	derp	=numbarrrr;
	int	ret		=1;

	if(numbarrrr < 0)
	{
		ret++;	//extra spot for negative sign if you want?
	}

	while(derp > 9 || derp < -9)
	{
		derp	/=10;
		ret++;
	}
	return	ret;
}


void	main(void)
{
	time_t	tyme;

	srand(time(&tyme));

	int	halfRMAX	=RAND_MAX >> 1;

	for(int i=0;i < 5;i++)
	{
		int	noombar	=rand() - halfRMAX;
		printf("blert %d, with %d digitsesesesse\n", noombar, CountDigitses(noombar));		
	}
}