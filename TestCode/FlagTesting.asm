;x64 asm for testing flags and other stuff I'm making
		global		main
		extern		puts
		extern		exit

section	.text
main:
		;for linux, puts expects this argument in rdi
		;not really sure which is correct
		mov		rcx, message
		call	puts

		;exit is needed here on windows or ret jumps into
		;garbage code, not sure if that is nasm's fault or what
		call	exit

section	.data
message:
		db			'Goblinoids', 0
