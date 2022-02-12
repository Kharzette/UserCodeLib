//blort!  Superbasic test program
uint16	goblins

//specify how large to make the data page in bytes
#pragma		DataPageSize		1024

	mov		goblins, 69
	mov		reg00, goblins
	addrOf	reg01, goblins
	mov		reg02, [reg01]
	add		reg00, [reg01]
shart:
	inc		reg02
	dec		reg00
	tst		reg00, reg00
	jnz		shart
	mul		reg02, 7