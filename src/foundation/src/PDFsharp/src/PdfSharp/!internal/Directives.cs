// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

//
// Documentation of conditional compilation symbols used in PDFsharp.
// Checks correct settings and obsolete conditional compilation symbols.
//

#if !DEBUG && TEST_CODE
#warning ***********************************************************
#warning ***** ‘TEST_CODE’ MUST BE UNDEFINED FOR FINAL RELEASE *****
#warning ***********************************************************
#endif

#if !DEBUG && PDFSHARP_DEBUG
#warning ****************************************************************
#warning ***** ‘PDFSHARP_DEBUG’ MUST BE UNDEFINED FOR FINAL RELEASE *****
#warning ****************************************************************
#endif




// PDFsharp independent of any particular .NET library.

