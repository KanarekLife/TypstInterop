fn main() {
    csbindgen::Builder::default()
        .input_extern_file("src/lib.rs")
        .csharp_class_name("NativeMethods")
        .csharp_namespace("TypstInterop.Internal")
        .csharp_dll_name("typst_interop")
        .generate_csharp_file("../TypstInterop/Internal/NativeMethods.g.cs")
        .unwrap();
}
