// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using PdfSharp.Pdf.Advanced;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PdfSharp.Pdf
{
    /// <summary>
    /// Base class for both ArrayElements and DictionaryElements.
    /// </summary>
    /// <param name="owningContainer">The PdfContainer this instance belongs to.</param>
    public abstract class ElementsBase(PdfContainer owningContainer)
    {
        /// <summary>
        /// Gets the PDF array or dictionary the elements belong to.
        /// </summary>
        public PdfContainer OwningContainer { get; internal set; } = owningContainer;

        /// <summary>
        /// Marks a PDF item as 'not in use' anymore.
        /// For a PDF reference or an indirect object the reference counter is decremented.
        /// (Note that the reference counter is not yet used in PDFsharp.)
        /// For a direct object the structure parent is set to null and the object can be
        /// reused as a direct object or became an indirect object.
        /// </summary>
        protected static void ReleaseItem(PdfItem item)
        {
            //if (item == null)
            //    return;
            if (item is PdfObject obj)
            {
                var reference = obj.Reference;
                if (reference != null)
                {
                    Debug.Assert(obj.ParentInfo == null, "Indirect PDF object must not have a ParentInfo.");
                    reference.Release();
                }
                else
                {
                    Debug.Assert(obj.ParentInfo != null, "Direct PDF object must have a ParentInfo.");
                    obj.SetStructureParentNull();
                    // The object is not dead and can be reused.
                }
            }
            else if (item is PdfReference reference)
            {
                // reference.Value can be null during reading a PDF document.
                Debug.Assert(reference.Value?.ParentInfo == null, "Indirect PDF object must not have a ParentInfo.");
                reference.Release();
            }
        }

        /// <summary>
        /// Creates a container of the specified type depending on an optional old container.
        /// </summary>
        /// <param name="type">The type of the object to be created.</param>
        /// <param name="oldContainer">Container its elements are moved to the newly created type.</param>
        /// <param name="createIndirect">If true creates an indirect object.</param>
        protected internal PdfContainer CreateContainer(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
            Type type, PdfContainer? oldContainer, bool createIndirect)
        {
            PdfContainer cont;
            if (oldContainer == null)
            {
                // Try to get constructor with signature 'Ctor(PdfDocument, bool)'.
                var ctorInfo = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, [typeof(PdfDocument), typeof(bool)], null);

                if (ctorInfo != null)
                {
                    cont = (PdfContainer)ctorInfo.Invoke([OwningContainer.Owner, false]);
                }
                else
                {
                    // Try to get constructor with signature 'Ctor(PdfDocument)'.
                    ctorInfo = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, [typeof(PdfDocument)], null);
                    if (ctorInfo != null)
                    {
                        cont = (PdfContainer)ctorInfo.Invoke([OwningContainer.Owner]);
                    }
                    else
                    {
                        // Try to get with signature 'Ctor()'.
                        ctorInfo = type.GetConstructor(
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                            null, [], null);

                        if (ctorInfo != null)
                        {
                            cont = (PdfContainer)ctorInfo.Invoke([]);
                        }
                        else
                            throw new InvalidOperationException($"No appropriate constructor found for type {type.FullName}.");
                    }
                }
                Debug.Assert(cont?.IsTransformed is false);
                cont.SetTransformed();
            }
            else
            {
                if (type.IsInstanceOfType(oldContainer)) 
                    Debug.Assert(false, "Should not happen.");

                ConstructorInfo? ctorInfo;
                if (typeof(PdfDictionary).IsAssignableFrom(type))
                {
                    // Use constructor with signature 'Ctor(PdfDictionary array)'.
                    ctorInfo = type.GetConstructor(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, types: [typeof(PdfDictionary)], null);
                }
                else if (typeof(PdfArray).IsAssignableFrom(type))
                {
                    // Use constructor with signature 'Ctor(PdfArray dict)'.
                    ctorInfo = type.GetConstructor(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, types: [typeof(PdfArray)], null);
                }
                else
                {
                    if (type == typeof(PdfRectangle))
                    {
                        // IMPROVE see US291
                        throw new InvalidOperationException("PdfRectangle is not implemented as an array, try use GetRectangle.");
                    }
                    throw new InvalidOperationException($"Type {type.Name} is not allowed here");
                }

                Debug.Assert(ctorInfo != null, $"No appropriate constructor found for type: {type.Name}.");
                cont = (PdfContainer)ctorInfo.Invoke([oldContainer]);
                //Debug.Assert(dict?.IsTransformed is false);
                if (cont?.IsTransformed == false)
                {
                    Debugger.Break();
                    cont?.SetTransformed();
                }
            }
            Debug.Assert(cont?.IsTransformed is true);
            return cont ?? NRT.ThrowOnNull<PdfContainer>();
        }


        /// <summary>
        /// Ensures that the specified value type is at least
        /// of the specified generic type.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void EnsureValueType<T>(Type valueType)
        {

            if (typeof(T).IsAssignableFrom(valueType))
                return;
            ThrowInappropriateValueType(typeof(T), valueType);
        }

        void ThrowInappropriateValueType(Type required, Type specified)
        {
            throw new InvalidOperationException($"The specified value type '{specified.FullName}' is not derived from '{required.FullName}'.");
        }

        /// <summary>
        /// Must not use direct primitive objects.
        /// Use e.g. PdfString instead of PdfStringObject.
        /// </summary>
        protected static void FailForDirectPrimitiveObject(PdfObject obj)
        {
            // Case: Direct primitive object
            // Use e.g. PdfString instead of PdfStringObject.

            Debug.Assert(obj is PdfPrimitiveObject);

            var name = obj.GetType().Name;
            var replaceType = "(unknown)";
            if (!name.EndsWith("Object", StringComparison.Ordinal))
                throw new InvalidOperationException("Use Array, Dictionary, or Simple type.");
            replaceType = name[..^"Object".Length];
            var message = $"{name} can only be used as an indirect object. Use {replaceType} instead.";
            throw new InvalidOperationException(message);
        }
    }
}
