/// The "iterator" we pass to `C#`
#[repr(C)]
pub struct CSharpIteratorOut<T: Sized + Default> {
    /// The function we pass `C#`. It's called by `C#` and recieves the 
    /// pointer to the `Box`ed iterator
    internal_iter: extern "C" fn(*mut Box<dyn Iterator<Item=T>>, *mut T) -> bool,
    /// A thin pointer to the fat iterator pointer that gets leaked
    pointer: *mut Box<dyn Iterator<Item=T>>
}

/// A stock function that handles iterator work. This is unsafe.
pub extern fn iter_impl_ffi<T: Sized + Default + std::fmt::Debug>(p: *mut Box<dyn Iterator<Item=T>>, data: *mut T) -> bool{
    unsafe {
        match (*p).next() {
            // If there is new data...
            Some(x) => {
                // Write it to the pointer we got...
                *data = x;
                // And tell `C#` that it can poll again
                true
            }
            // If there isn't any new data...
            None => {
                // Drop iterator automatically, C# will have to sanity check
                let _ = Box::from_raw(p); 
                // And tell `C#` to not poll again
                false
            }
        }
    }
}

impl<T: Sized + Default + std::fmt::Debug> CSharpIteratorOut<T> {
    /// Creates a `CSharpIteratorOut<T>` from an iterator over `T`
    pub fn form<D: Iterator<Item=T> + 'static>(iter: D) -> Self {
        CSharpIteratorOut {
            // Uses the stock function
            internal_iter: iter_impl_ffi,
            // Leaks the pointer so that it doesn't get dropped until 
            // we get a None value in `iter_impl_ffi`
            pointer: Box::into_raw(Box::new(Box::new(iter) as _))
        }
    }
}

/// An example function:
/// 
/// Creates an `Iterator<Item=Vec<usize>>` with each one counting up 
/// to the current iteration
#[no_mangle]
pub extern fn get_iterator(cs: &mut CSharpIteratorOut<Vec<usize>>) {
    let data = 0..40;
    let iterator = CSharpIteratorOut::form(data.map(|x| {(0..x).collect::<Vec<usize>>()}));
    *cs = iterator;
}
