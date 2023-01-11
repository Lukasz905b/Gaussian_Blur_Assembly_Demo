.code
BlurImageAsm proc uses R12 R13 R14 R15
; initial values

; byte image[]			in RCX
; byte result_image[]	in RDX
; byte mask[]			in R8
; int mask_value		in R9
MOV R12, RCX; move image pointer to R12
MOV R13, RDX; move result_image pointer to R13
MOV R10, QWORD PTR [RSP + 8 * 9]; move the width value to R10
MOV R11, QWORD PTR [RSP + 8 * 10]; move the height value to R11
CVTSI2SS XMM2, R9; move sum of mask weights to XMM2 for later division

; calculate real image width in bytes
ADD R10, 2; add 2 to the width to account for padding
MOV RAX, 4; move 4 to RAX to prepare for multiplication 
MUL R10; multiply width by 4 to get image width in bytes
MOV R10, RAX; move image width in bytes back to R10

; calculate real image size in bytes
ADD R11, 2; add 2 to the height to account for padding
MUL R11; multiply height with width to get image size; image width is already in RAX due to a previous operation
MOV R11, RAX; move image size in bytes to R11

; reserve space on stack for local variables
SUB RSP, 16

; prepare the mask in an XMM register
MOV RAX, QWORD PTR [R8]; move first 8 bytes of the mask to RAX
MOV [RSP], RAX; move first 8 bytes of the mask to the stack
XOR RAX, RAX; fill RAX with zeroes
MOV AL, BYTE PTR [R8 + 8]; move the last byte of the mask to AL
MOV [RSP + 8], RAX; move the last byte of the mask to the stack
MOVDQU XMM0, XMMWORD PTR [RSP]; move the entire padded mask to XMM0

; initialize main loop variables
MOV R14, R10; move the initial value of i to R14
MOV R15, 0; move the initial value of j to R15

; main loop of the program
mainloop:

; main loop condition check
MOV RAX, R11; move image size to RAX for subtraction
SUB RAX, R10; subtract width from image size to discard bottom padding
CMP RAX, R14; check if the entire image has been iterated over
JE programend; if the entire image has been processed jump to the end

; check if i is pointing to a left side padding pixel
MOV RAX, R14; move the value of i to RAX for division
MOV RDX, 0; empty the RDX register which will store the remainder of the division
DIV R10; divide i with width
CMP RDX, 0; check if division remainder is 0
JE paddingdetected; if i % width == 0 padding has been detected

; check if i is pointing to a right side padding pixel
MOV RAX, R14; move the value of i to RAX for division
MOV RDX, 0; empty the RDX register which will store the remainder of the division
DIV R10; divide i with width
ADD RDX, 4; add 4 to the remainder to simulate subtracting 4 from width
CMP RDX, R10; check if division remainder is equal to width - 4
JE paddingdetected; if i % width == (width - 4) padding has beed detected

; offset image address to the index of the top left
SUB R12, R10; subtract the width from image address
SUB R12, 4; subtract the size of one pixel from image address
ADD R12, R14; add current i value to image address

; load one color value of all pixels under the mask
MOV AL, BYTE PTR [R12 + 2*R10 + 4]; load one bottom middle pixel color value to AL
SHL RAX, 8; shift RAX to the left to load in more pixel colors

MOV AL, BYTE PTR [R12 + 2*R10]; Load one bottom left pixel color value to AL
SHL RAX, 8; shift RAX to the left to load in more pixel colors

MOV AL, BYTE PTR [R12 + R10 + 8]; load one middle right pixel color value to AL
SHL RAX, 8; shift RAX to the left to load in more pixel colors

MOV AL, BYTE PTR [R12 + R10 + 4]; load one center pixel color value to AL
SHL RAX, 8; shift RAX to the left to load in more pixel colors

MOV AL, BYTE PTR [R12 + R10]; Load one middle left color value to AL
SHL RAX, 8; shift RAX to the left to load in more pixel colors

MOV AL, BYTE PTR [R12 + 8]; load one top right pixel color value to AL
SHL RAX, 8; shift RAX to the left to load in more pixel colors

MOV AL, BYTE PTR [R12 + 4]; load one top middle pixel color value to AL
SHL RAX, 8; shift RAX to the left to load in more pixel colors

MOV AL, BYTE PTR [R12]; Load one top left pixel color value to AL
MOVQ XMM1, RAX; move 8 pixel values to the stack

XOR RAX, RAX; empty the RAX register
MOV AL, BYTE PTR [R12 + 2*R10 + 8]; load one bottom right pixel color value to AL
MOVQ XMM3, RAX; load last pixel color to the stack
MOVLHPS XMM1, XMM3

; restore image address back to the original value
ADD R12, R10; add image width back to image address
ADD R12, 4; add pixel size back to image address
SUB R12, R14; subtract current i value from image address

; multiply color values with corresponing mask weights
;MOVDQU XMM1, XMMWORD PTR [RSP]; load pixel color values to XMM1
PMADDUBSW XMM1, XMM0; multiply each color value with the corresponding mask weight

; add results
XOR RAX, RAX; empty the RAX register
XOR RDX, RDX; empty the RDX register
MOVQ RCX, XMM1; move first four result values to RCX
MOVHLPS XMM3, XMM1; move last result value to low part of XMM2
MOVQ RAX, XMM3; move last result value to RAX
MOV DX, CX; move first value to DX
ADD EAX, EDX; add first value to sum
SHR RCX, 16; move second value to CX
MOV DX, CX; move second value to DX
ADD EAX, EDX; add second value to sum
SHR RCX, 16; move third value to CX
MOV DX, CX; move third value to DX
ADD EAX, EDX; add third value to sum
SHR RCX, 16; move fourth value to CX
MOV DX, CX; move fourth value to DX
ADD EAX, EDX; add fourth value to sum

; divide results by sum of mask values rounding down
CVTSI2SS XMM3, EAX; move sum to XMM3 for division
DIVSS XMM3, XMM2; divide sum by sum of mask weights
CVTSS2SI EAX, XMM3; convert result value back to an integer
MOV BYTE PTR [R13 + R15], AL; move final color value to the result image

; repeat loop
ADD R15, 1; increment j
ADD R14, 1; increment i
JMP mainloop; go back to the start of the loop

; skip over padding pixels
paddingdetected:
ADD R14, 4; add 4 to i to skip over current pixel
JMP mainloop; go back to the beginning of the loop

; end procedure
programend:
ADD RSP, 16
MOV rax, 0
ret
BlurImageAsm endp
end