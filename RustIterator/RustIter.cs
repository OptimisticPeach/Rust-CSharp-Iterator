using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace RustIterator
{
    /// <summary>
    /// The iterator that rust will be sending us, the core of this example.
    /// This is a specialized struct in both rust AND C#
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RustFFIIterator
    {
        /// <summary>
        /// A pointer to a function that contains
        /// a call to the internal iterator's next() function
        /// </summary>
        /// <remarks>
        /// Equivalent to a RustIteratorNext. If there is another
        /// value, then it will initialize the reference, otherwise
        /// it will destroy the iterator and return false
        /// </remarks>
        public IntPtr Next;
        /// <summary>
        /// A pointer to a pointer to our iterator. 
        /// </summary>
        /// <remarks>
        /// Needs two layers of indirection because rust treats 
        /// references to trait objects as a pair of pointers,
        /// so it's easier to just pass a pointer to the pair.
        /// This shouldn't be touched in C#
        /// </remarks>
        public IntPtr Iterator;
    }
    /// <summary>
    /// The model for our iterator's next function, 
    /// <seealso cref="RustFFIIterator.Next"/> for a pointer to it
    /// </summary>
    /// <param name="iter">
    /// A pointer to the iterator itself.
    /// <see cref="RustFFIIterator.Iterator"/>
    /// </param>
    /// <param name="a">
    /// A pointer to the data that it will initialize. 
    /// This uses a bit of pointer magic
    /// </param>
    /// <returns>
    /// If there are any more objects to return.
    /// </returns>
    /// <remarks>
    /// If this returns false, don't call it again, 
    /// as the rust iterator is now dropped
    /// </remarks>
    public unsafe delegate bool RustIteratorNext(IntPtr iter, void* a);
    /// <summary>
    /// A smart manager for a rust iterator in C#. Allows
    /// for iteration just like an iterator would in rust.
    /// </summary>
    /// <typeparam name="T">
    /// The type that is being iterated over. Note that this 
    /// must be a sized struct that doesn't contain a class.
    /// </typeparam>
    public class RustIter<T> : IEnumerable<T> where T: struct
    {
        /// <summary>
        /// An internal copy of the iterator that rust sent over.
        /// </summary>
        private RustFFIIterator ffiiter;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="d">
        /// Iterator from rust
        /// </param>
        public RustIter(RustFFIIterator d)
        {
            ffiiter = d;
        }

        #region IEnumerator
        public IEnumerator<T> GetEnumerator()
        {
            return new REnumerator(ffiiter);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new REnumerator(ffiiter);
        }
        #endregion
        /// <summary>
        /// Rust enumerator; takes care of marshalling stuff
        /// </summary>
        struct REnumerator : IEnumerator, IEnumerator<T>
        {
            /// <summary>
            /// We don't actually have an internal object, we
            /// have a handle to an object that is guaranteed
            /// to be of type T, so casting is okay. This is 
            /// used to keep a pinned object while iterating.
            /// </summary>
            private GCHandle handle;
            /// When the iterator is done, it'll automatically 
            /// destroy it leaving us with a dangling pointer,
            /// so we have to track the state to not throw an
            /// inter-ffi exception and a rust unwind
            private bool ended;
            /// <summary>
            /// The actual function that is called to get the
            /// next value
            /// </summary>
            private RustIteratorNext next;
            /// <summary>
            /// The pointer to the iterator, copied from the 
            /// <see cref="RustFFIIterator"/> that we recieve
            /// </summary>
            private IntPtr iterPtr;
            
            /// <summary>
            /// Because we don't own a T, we cast the guaranteed
            /// T behind the handle we own
            /// </summary>
            public object Current => (T)handle.Target;

            T IEnumerator<T>.Current => (T)handle.Target;

            public REnumerator(RustFFIIterator d)
            {
                /// A default value
                var data = default(T);
                /// We pin the value
                handle = GCHandle.Alloc(data, GCHandleType.Pinned);
                /// We initialize the next() function
                next = Marshal.GetDelegateForFunctionPointer<RustIteratorNext>(d.Next);
                /// Store the iterator
                iterPtr = d.Iterator;
                /// We still haven't ended, since this is the constructor
                ended = false;
            }

            public void Dispose()
            {
                /// Unpin this' current value
                handle.Free();
            }

            public bool MoveNext()
            {
                /// Don't try to call a function on a dangling pointer.
                if (!ended)
                {
                    bool worked;
                    unsafe
                    {
                        worked = next(iterPtr, (void*)handle.AddrOfPinnedObject());
                    }
                    if (!worked)
                    {
                        ended = true;
                    }
                    return worked;
                }
                return !ended;
            }

            /// <summary>
            /// Nonfunctional because we can't reset a rust iterator
            /// </summary>
            public void Reset()
            {
                throw new NotImplementedException();
            }
        }
    }
}
