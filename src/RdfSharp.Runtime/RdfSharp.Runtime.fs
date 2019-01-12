namespace Iride

open System

type UriFactory() = 
    static member Create uri = Uri uri


// Put the TypeProviderAssemblyAttribute in the runtime DLL, pointing to the design-time DLL
[<assembly:CompilerServices.TypeProviderAssembly("RdfSharp.DesignTime.dll")>]
do ()
