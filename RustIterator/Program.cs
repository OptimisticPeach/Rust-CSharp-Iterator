using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace RustIterator
{
    /// <summary>
    /// Rust Vector, ready to be passed from an ffi into C# because of its identical memory model
    /// </summary>
    /// <example>
    /// [DllImport("rust_lib.dll")]
    /// private static extern void get_vec(out Vec RsVector);
    /// 
    /// var v = new Vec();
    /// get_vec(out v);
    /// </example>
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct Vec
    {
        /// <summary>
        /// The pointer to the data on the heap that the Vec&lt;T&gt; points to.
        /// </summary>
        /// <remarks>
        /// This is in this place, because of the following chain of equivalences:
        /// Vec&lt;T&gt;: https://doc.rust-lang.org/src/alloc/vec.rs.html#306-309
        /// └buf: RawVec&lt;T&gt;: https://doc.rust-lang.org/1.26.0/src/alloc/raw_vec.rs.html#50-54
        ///  └ptr: Unique&lt;T&gt;: https://doc.rust-lang.org/1.26.2/src/core/ptr.rs.html#2514-2522
        ///   └pointer: NonZero&lt;*const T&gt;: https://doc.rust-lang.org/1.22.1/src/core/nonzero.rs.html#66
        ///    └.0: *const T
        /// </remarks>
        public IntPtr ptr;
        /// <summary>
        /// The full capacity of the Vec&lt;T&gt;; unused because not all of it is guaranteed to be initialized
        /// </summary>
        /// <remarks>
        /// This is in this place, because of the following chain of equivalences:
        /// Vec&lt;T&gt;: https://doc.rust-lang.org/src/alloc/vec.rs.html#306-309
        /// └buf: RawVec&lt;T&gt;: https://doc.rust-lang.org/1.26.0/src/alloc/raw_vec.rs.html#50-54
        ///  └cap: usize
        /// </remarks>
        public ulong capacity;
        /// <summary>
        /// The length of the used memory of the Vec&lt;T&gt;'s 
        /// currently allocated memory; therefore guaranteed 
        /// to be initialized
        /// </summary>
        /// <remarks>
        /// This is in this place, because of the following chain of equivalences:
        /// Vec&lt;T&gt;: https://doc.rust-lang.org/src/alloc/vec.rs.html#306-309
        /// └len: usize
        /// </remarks>
        public ulong size;

        /// <summary>
        /// Because we can't infer the type for this, we just pretty-print the pointer, capacity and length
        /// </summary>
        /// <returns>
        /// A pretty-printed version of rust's Vec&lt;T&gt; without knowledge of &lt;T&gt;
        /// </returns>
        public override string ToString()
        {
            return "ptr: " + ptr.ToInt64().ToString("x") + "\ncap: " + capacity + "\nsize: " + size;
        }
        /// <summary>
        /// Using a caller-provided type, creates a List&lt;T&gt; using some Marshalling
        /// </summary>
        /// <typeparam name="T">
        /// The type that the pointer is to be interpreted as; 
        /// if it's incorrect in size, THIS WILL CRASH
        /// </typeparam>
        /// <returns>
        /// A re-interpretation of the pointer's data turned into a List&lt;T&gt;
        /// </returns>
        public List<T> ToList<T>() where T : struct
        {
            /// First allocate a byte array because Marshal.Copy doesn't have a pointer to pointer variant
            var arr = new byte[size * (ulong)Marshal.SizeOf<T>()];
            /// Copy the bytes from the rust vec to the buffer we just created
            Marshal.Copy(ptr, arr, 0, arr.Length);
            /// Prepare an array of &lt;T&gt;
            var narr = new T[size];
            /// Pin the new array so that we can write to it
            var handle = GCHandle.Alloc(narr, GCHandleType.Pinned);
            /// Copy using some IntPtrs
            Marshal.Copy(arr, 0, handle.AddrOfPinnedObject(), (int)(size * (ulong)Marshal.SizeOf<T>()));
            /// Release the pinned array
            handle.Free();
            /// Turn it into a list, this could technically return an array
            return narr.ToList();
        }
    }
    class Program
    {
        /// <summary>
        /// Our external function that gets us an iterator
        /// </summary>
        /// <param name="iter">
        /// A reference to an uninitialized iterator
        /// </param>
        [DllImport("cs_iter.dll")]
        private static extern void get_iterator(out RustFFIIterator iter);
        static void Main(string[] args)
        {
            /// Uninitialized iterator
            var iter = new RustFFIIterator();
            /// Initialize it in rust
            get_iterator(out iter);
            /// Create our special iterator for rust objects
            var i = new RustIter<Vec>(iter);
            /// Loop
            foreach (var b in i)
            {
                /// Loop on the list that rust sends us
                foreach (var c in b.ToList<ulong>())
                {
                    /// Print it
                    Console.Write(c + " ");
                }
                Console.WriteLine();
            }
            Console.ReadKey();
        }
    }
}
