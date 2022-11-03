namespace TaggedUnions
{
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    public interface ITaggedUnion
    {
        internal nint OffsetToBuffer { get; }
        internal byte Tag { get; set; }
    }

    // The convention is TaggedUnion_X_Y,
    // where X is the number of reference types, and Y is the number of bytes of storage.
    // The size of the tagged union then is ptrsize * X + Y + 1 byte for the tag.
    // (Y + 1) should be a multiple of ptrsize to not waste space on padding.
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct TaggedUnion_3_15 : ITaggedUnion
    {
        // Reference types
        internal object? _ref0;
        internal object? _ref1;
        internal object? _ref2;

        // Fixed size buffer
        internal ulong _byte0_7;
        internal uint _byte8_11;
        internal byte _byte12;
        internal byte _byte13;
        internal byte _byte14;
        
        internal byte _tag;

        readonly nint ITaggedUnion.OffsetToBuffer => OffsetToBuffer;
        byte ITaggedUnion.Tag { readonly get => _tag; set => _tag = value; }

        public static readonly nint OffsetToBuffer = (nint) (3 * IntPtr.Size);
    }

    public static unsafe class GenericImpl
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref V _GetStructManaged<T, V>(ref T u, nint offset)
            where T : struct
            // where V : struct
        {
            ref byte b = ref Unsafe.As<T, byte>(ref u);
            ref byte t = ref Unsafe.AddByteOffset(ref b, offset);
            return ref Unsafe.As<byte, V>(ref t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref V _GetStructUnmanaged<T, V>(ref T u)
            where T : struct, ITaggedUnion
            where V : unmanaged
        {
            ref byte b = ref Unsafe.As<T, byte>(ref u);
            ref byte t = ref Unsafe.AddByteOffset(ref b, u.OffsetToBuffer);
            return ref Unsafe.As<byte, V>(ref t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref V _GetClass<T, V>(ref T u)
            where T : struct
            // where V : class
        {
            return ref Unsafe.As<T, V>(ref u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref V _GetClass<T, V>(in T u, byte tag, nint refTypesOffset)
            where T : struct, ITaggedUnion
            // where V : class
        {
            Debug.Assert(tag == u.Tag);
            Debug.Assert(u.OffsetToBuffer > refTypesOffset);
            return ref Unsafe.As<T, V>(ref Unsafe.AsRef(u));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref V _GetStruct<T, V>(in T u, byte tag, nint refTypesOffset)
            where T : struct, ITaggedUnion
            // where V : struct
        {
            Debug.Assert(tag == u.Tag);
            Debug.Assert(u.OffsetToBuffer > refTypesOffset);
            ref V result = ref GenericImpl._GetStructManaged<T, V>(ref Unsafe.AsRef(u), u.OffsetToBuffer - refTypesOffset);
            return ref result;
        }

        internal static bool TryGetStruct<T, V>(in T u, Tag<T, V> tag, out V? outValue)
            where T : struct, ITaggedUnion
            // where V : struct
        {
            if (u.Tag != tag.Value)
            {
                outValue = default;
                return false;
            }
            outValue = _GetStruct<T, V>(u, tag.Value, tag.RefTypesOffset);
            return true;
        }

        internal static bool TryGetClass<T, V>(in T u, Tag<T, V> tag, out V? outValue)
            where T : struct, ITaggedUnion
            // where V : class
        {
            if (u.Tag != tag.Value)
            {
                outValue = default;
                return false;
            }
            outValue = GenericImpl._GetClass<T, V>(u, tag.Value, tag.RefTypesOffset);
            return true;
        }

        internal static T _FromStruct<T, V>(byte tag, nint offset, ref V value)
            where T : struct, ITaggedUnion
            // where V : struct
        {
            T t = new T();
            t.Tag = tag;

            ref V v = ref _GetStruct<T, V>(t, tag, offset);
            v = value;
            return t;
        }

        internal static T _FromClass<T, V>(byte tag, V? value)
            where T : struct, ITaggedUnion
            // where V : class
        {
            T t = new T();
            t.Tag = tag;

            ref V v = ref _GetClass<T, V>(ref t);
            v = value!;
            return t;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T New<T, V>(Tag<T, V> tag, ref V? value)
            where T : struct, ITaggedUnion
            // where V : struct
        {
            return New<T, V>(tag.Value, tag.RefTypesOffset, ref value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T New<T, V>(byte tagValue, nint refTypesOffset, ref V? value)
            where T : struct, ITaggedUnion
            // where V : struct
        {
            if (typeof(V).IsValueType)
                return _FromStruct<T, V>(tagValue, refTypesOffset, ref value!);
            else
                return _FromClass<T, V>(tagValue, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGet<T, V>(in T u, Tag<T, V> tag, out V? outValue)
            where T : struct, ITaggedUnion
            // where V : struct
        {
            return TryGet<T, V>(u, tag.Value, tag.RefTypesOffset, out outValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGet<T, V>(in T u, byte tagValue, nint refTypesOffset, out V? outValue)
            where T : struct, ITaggedUnion
            // where V : struct
        {
            if (u.Tag != tagValue)
            {
                outValue = default;
                return false;
            }
            if (typeof(V).IsValueType)
                outValue = _GetStruct<T, V>(u, tagValue, refTypesOffset);
            else
                outValue = _GetClass<T, V>(u, tagValue, refTypesOffset);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static V? Get<T, V>(in T u, Tag<T, V> tag)
            where T : struct, ITaggedUnion
        {
            if (TryGet(u, tag, out V? outValue))
                return outValue;
            throw new WrongTagException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static V? Get<T, V>(in T u, byte tagValue, nint refTypesOffset)
            where T : struct, ITaggedUnion
        {
            if (TryGet(u, tagValue, refTypesOffset, out V? outValue))
                return outValue;
            throw new WrongTagException();
        }
    }

    public static class TagHelper<TValue>
    {
        public static readonly nint RefTypesOffset = _RefTypesOffset();
        private static nint _RefTypesOffset()
        {
            if (!typeof(TValue).IsValueType
                || typeof(TValue).IsPrimitive
                || typeof(TValue).IsEnum)
            {
                return 0;
            }
            else
            {
                return typeof(TValue).GetFields(
                        System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic)
                    .Count(f => !f.FieldType.IsEnum && !f.FieldType.IsValueType) * IntPtr.Size;
            }
        }
        public static nint GetRefTypesOffsetWithCheck<TTaggedUnion>()
            where TTaggedUnion : struct, ITaggedUnion
        {
            Debug.Assert(CheckRefTypeSizeGood<TTaggedUnion>());
            return RefTypesOffset;
        }

        public static bool CheckRefTypeSizeGood<TTaggedUnion>()
            where TTaggedUnion : struct, ITaggedUnion
        {
            return default(TTaggedUnion).OffsetToBuffer >= RefTypesOffset;
        }
    }

    public class Tag<TTaggedUnion, TValue>
        where TTaggedUnion : struct, ITaggedUnion
    {
        internal byte Value;
        internal nint RefTypesOffset;

        public Tag(byte value)
        {
            Value = value;
            RefTypesOffset = TagHelper<TValue>.GetRefTypesOffsetWithCheck<TTaggedUnion>();
        }

        public Tag(byte value, nint refTypesOffset)
        {
            Value = value;
            RefTypesOffset = refTypesOffset;
            Debug.Assert(default(TTaggedUnion).OffsetToBuffer > RefTypesOffset);
        }
    }

    public class WrongTagException : Exception
    {
    }

    // These need to be defined for every tagged union type
    public static class BoilerplateExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGet<V>(this in TaggedUnion_3_15 u, Tag<TaggedUnion_3_15, V> tag, out V? outValue)
        {
            return GenericImpl.TryGet(u, tag, out outValue);
        } 

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static V? Get<V>(this in TaggedUnion_3_15 u, Tag<TaggedUnion_3_15, V> tag)
        {
            return GenericImpl.Get(u, tag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TaggedUnion_3_15 New<V>(this Tag<TaggedUnion_3_15, V> tag, V? value)
        {
            return GenericImpl.New(tag, ref value);
        }
        
        public static TReturn Match<TReturn, V1>(
            this in TaggedUnion_3_15 u,
            Tag<TaggedUnion_3_15, V1> tag1, Func<V1?, TReturn> f1)
        {
            if (TryGet(u, tag1, out V1? v1))
                return f1(v1);
            throw new WrongTagException();
        }

        public static TReturn Match<TReturn, V1>(
            this in TaggedUnion_3_15 u,
            Tag<TaggedUnion_3_15, V1> tag1, Func<V1?, TReturn> f1,
            Func<TReturn> defaultCase)
        {
            if (TryGet(u, tag1, out V1? v1))
                return f1(v1);
            return defaultCase();
        }

        public static TReturn Match<TReturn, V1, V2>(
            this in TaggedUnion_3_15 u, 
            Tag<TaggedUnion_3_15, V1> tag1, Func<V1?, TReturn> f1,
            Tag<TaggedUnion_3_15, V2> tag2, Func<V2?, TReturn> f2)
        {
            if (TryGet(u, tag1, out V1? v1))
                return f1(v1);
            if (TryGet(u, tag2, out V2? v2))
                return f2(v2);
            throw new WrongTagException();
        }

        public static TReturn Match<TReturn, V1, V2>(
            this in TaggedUnion_3_15 u, 
            Tag<TaggedUnion_3_15, V1> tag1, Func<V1?, TReturn> f1,
            Tag<TaggedUnion_3_15, V2> tag2, Func<V2?, TReturn> f2,
            Func<TReturn> defaultCase)
        {
            if (TryGet(u, tag1, out V1? v1))
                return f1(v1);
            if (TryGet(u, tag2, out V2? v2))
                return f2(v2);
            return defaultCase();
        }
        
        public static TReturn Match<TReturn, V1, V2, V3>(
            this in TaggedUnion_3_15 u, 
            Tag<TaggedUnion_3_15, V1> tag1, Func<V1?, TReturn> f1,
            Tag<TaggedUnion_3_15, V2> tag2, Func<V2?, TReturn> f2,
            Tag<TaggedUnion_3_15, V3> tag3, Func<V3?, TReturn> f3)
        {
            if (TryGet(u, tag1, out V1? v1))
                return f1(v1);
            if (TryGet(u, tag2, out V2? v2))
                return f2(v2);
            if (TryGet(u, tag3, out V3? v3))
                return f3(v3);
            throw new WrongTagException();
        }

        public static TReturn Match<TReturn, V1, V2, V3>(
            this in TaggedUnion_3_15 u, 
            Tag<TaggedUnion_3_15, V1> tag1, Func<V1?, TReturn> f1,
            Tag<TaggedUnion_3_15, V2> tag2, Func<V2?, TReturn> f2,
            Tag<TaggedUnion_3_15, V3> tag3, Func<V3?, TReturn> f3,
            Func<TReturn> defaultCase)
        {
            if (TryGet(u, tag1, out V1? v1))
                return f1(v1);
            if (TryGet(u, tag2, out V2? v2))
                return f2(v2);
            if (TryGet(u, tag3, out V3? v3))
                return f3(v3);
            return defaultCase();
        }
    }

    // Types like this should be generated on demand.
    public struct TaggedUnion_3_15<V0, V1, V2, V3> : ITaggedUnion
    {
        private static readonly nint Type0RefTypesOffset = TagHelper<V0>.GetRefTypesOffsetWithCheck<TaggedUnion_3_15>();
        private static readonly nint Type1RefTypesOffset = TagHelper<V1>.GetRefTypesOffsetWithCheck<TaggedUnion_3_15>();
        private static readonly nint Type2RefTypesOffset = TagHelper<V2>.GetRefTypesOffsetWithCheck<TaggedUnion_3_15>();
        private static readonly nint Type3RefTypesOffset = TagHelper<V3>.GetRefTypesOffsetWithCheck<TaggedUnion_3_15>();

        public TaggedUnion_3_15(V0? value) { _taggedUnion = GenericImpl.New<TaggedUnion_3_15, V0>(0, Type0RefTypesOffset, ref value); }
        public TaggedUnion_3_15(V1? value) { _taggedUnion = GenericImpl.New<TaggedUnion_3_15, V1>(1, Type1RefTypesOffset, ref value); }
        public TaggedUnion_3_15(V2? value) { _taggedUnion = GenericImpl.New<TaggedUnion_3_15, V2>(2, Type2RefTypesOffset, ref value); }
        public TaggedUnion_3_15(V3? value) { _taggedUnion = GenericImpl.New<TaggedUnion_3_15, V3>(3, Type3RefTypesOffset, ref value); }

        private TaggedUnion_3_15 _taggedUnion;

        byte ITaggedUnion.Tag { get => _taggedUnion._tag; set => _taggedUnion._tag = value; }
        nint ITaggedUnion.OffsetToBuffer => TaggedUnion_3_15.OffsetToBuffer;

        public readonly V0? Value0 { get => GenericImpl.Get<TaggedUnion_3_15, V0>(_taggedUnion, 0, Type0RefTypesOffset); }
        public readonly V1? Value1 { get => GenericImpl.Get<TaggedUnion_3_15, V1>(_taggedUnion, 1, Type1RefTypesOffset); }
        public readonly V2? Value2 { get => GenericImpl.Get<TaggedUnion_3_15, V2>(_taggedUnion, 2, Type2RefTypesOffset); }
        public readonly V3? Value3 { get => GenericImpl.Get<TaggedUnion_3_15, V3>(_taggedUnion, 3, Type3RefTypesOffset); }

        public readonly bool TryGet0(out V0? value) { return GenericImpl.TryGet<TaggedUnion_3_15, V0>(_taggedUnion, 0, Type0RefTypesOffset, out value); }
        public readonly bool TryGet1(out V1? value) { return GenericImpl.TryGet<TaggedUnion_3_15, V1>(_taggedUnion, 1, Type1RefTypesOffset, out value); }
        public readonly bool TryGet2(out V2? value) { return GenericImpl.TryGet<TaggedUnion_3_15, V2>(_taggedUnion, 2, Type2RefTypesOffset, out value); }
        public readonly bool TryGet3(out V3? value) { return GenericImpl.TryGet<TaggedUnion_3_15, V3>(_taggedUnion, 3, Type3RefTypesOffset, out value); }
        public readonly bool TryGet<V>(out V? value)
        {
            if (typeof(V) == typeof(V0))
                return GenericImpl.TryGet(this._taggedUnion, 0, Type0RefTypesOffset, out value);
            if (typeof(V) == typeof(V1))
                return GenericImpl.TryGet(this._taggedUnion, 1, Type1RefTypesOffset, out value);
            if (typeof(V) == typeof(V2))
                return GenericImpl.TryGet(this._taggedUnion, 2, Type2RefTypesOffset, out value);
            if (typeof(V) == typeof(V3))
                return GenericImpl.TryGet(this._taggedUnion, 3, Type3RefTypesOffset, out value);
            Debug.Fail("Type " + typeof(V).Name + " wasn't one of the generic types");
            value = default;
            return false;
        }

        public readonly V? Get<V>()
        {
            if (TryGet<V>(out V? value))
                return value;
            throw new WrongTagException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly TReturn Match<TReturn>(
            Func<V0?, TReturn>? f0 = null,
            Func<V1?, TReturn>? f1 = null,
            Func<V2?, TReturn>? f2 = null,
            Func<V3?, TReturn>? f3 = null,
            Func<TReturn>? defaultCase = null)
        {
            if (f0 is null
                || f1 is null
                || f2 is null
                || f3 is null)
            {
                if (defaultCase is null)
                    Debug.Fail("Not all cases have been covered");
            }
            if (f0 is not null
                && f1 is not null
                && f2 is not null
                && f3 is not null)
            {
                // defaultCase is useless, should we leave it?
            }

            if (f0 is not null && TryGet0(out V0? v0))
                return f0(v0);
            if (f1 is not null && TryGet1(out V1? v1))
                return f1(v1);
            if (f2 is not null && TryGet2(out V2? v2))
                return f2(v2);
            if (f3 is not null && TryGet3(out V3? v3))
                return f3(v3);
            return defaultCase!();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Match(
            Action<V0?>? f0 = null,
            Action<V1?>? f1 = null,
            Action<V2?>? f2 = null,
            Action<V3?>? f3 = null,
            Action? defaultCase = null)
        {
            if (f0 is null
                || f1 is null
                || f2 is null
                || f3 is null)
            {
                if (defaultCase is null)
                    Debug.Fail("Not all cases have been covered");
                defaultCase();
                return;
            }
            if (f0 is not null
                && f1 is not null
                && f2 is not null
                && f3 is not null)
            {
                // defaultCase is useless, should we leave it?
            }

            if (f0 is not null && TryGet0(out V0? v0))
            {
                f0(v0);
                return;
            }
            if (f1 is not null && TryGet1(out V1? v1))
            {
                f1(v1);
                return;
            }
            if (f2 is not null && TryGet2(out V2? v2))
            {
                f2(v2);
                return;
            }
            if (f3 is not null && TryGet3(out V3? v3))
            {
                f3(v3);
                return;
            }
            defaultCase!();
        }

        // Implicit conversions
        public static implicit operator TaggedUnion_3_15<V0, V1, V2, V3>(V0? value) => new(value);
        public static implicit operator TaggedUnion_3_15<V0, V1, V2, V3>(V1? value) => new(value);
        public static implicit operator TaggedUnion_3_15<V0, V1, V2, V3>(V2? value) => new(value);
        public static implicit operator TaggedUnion_3_15<V0, V1, V2, V3>(V3? value) => new(value);
    }
}

namespace App
{
    using System.Diagnostics;
    using TaggedUnions;
    using MyConcreteTaggedUnion = TaggedUnions.TaggedUnion_3_15<int, string, ManagedStruct, UnmanagedStruct>;

    public struct ManagedStruct
    {
        public string str0;
        public string str1;
        public int int0;
        public int int1;
    }

    public struct UnmanagedStruct
    {
        public int int0;
        public int int1;
    }

    public struct Object<T>
    {
        public object Reference;
    }
    class Program
    {
        static void Main()
        {
            GenericTags();
        }

        // The bonus here is that the tags can be dynamic.
        // The bad thing is that since the id is stored as a byte, if tag registration has to be centralized,
        // otherwise the program could interpret memory wrong if not used carefully, which will most likely lead to a crash.
        // Alternatively we could tag the union by the tag pointer.
        // Then there should never be any issues, but it will take somewhat more space too.
        internal static void ExplicitTags()
        {
            var tag_int = new Tag<TaggedUnion_3_15, int>(value: 1, refTypesOffset: 0);
            var tag_obj = new Tag<TaggedUnion_3_15, object>(value: 2, refTypesOffset: 0);
            var tag_managed = new Tag<TaggedUnion_3_15, ManagedStruct>(value: 3, refTypesOffset: 2 * IntPtr.Size);
            var tag_unmanaged = new Tag<TaggedUnion_3_15, UnmanagedStruct>(value: 4, refTypesOffset: 0);

            // TaggedUnion_3_15 t0 = default;
            // nint base_ = ((nint) Unsafe.AsPointer(ref t0));
            // Console.WriteLine(IntPtr.Size);
            // Console.WriteLine("Ref0: " + (((nint) Unsafe.AsPointer(ref t0._ref0)) - base_));
            // Console.WriteLine("Ref1: " + (((nint) Unsafe.AsPointer(ref t0._ref1)) - base_));
            // Console.WriteLine("Ref2: " + (((nint) Unsafe.AsPointer(ref t0._ref2)) - base_));
            // Console.WriteLine("Buffer: " + (((nint) Unsafe.AsPointer(ref t0._byte0_7)) - base_));
            // Console.WriteLine("Tag: " + (((nint) Unsafe.AsPointer(ref t0._tag)) - base_));

            {
                var t = tag_managed.New(new ManagedStruct { int0 = 1, int1 = 2, str0 = "abc", str1 = "dce" });

                Console.WriteLine(t._tag);
                if (t.TryGet(tag_int, out var i))
                    Console.WriteLine("Int: " + i);
                else if (t.TryGet(tag_managed, out var m))
                    Console.WriteLine(m.str0);
            }

            {
                var t = tag_unmanaged.New(new UnmanagedStruct { int0 = 1, int1 = 2 });
                Console.WriteLine(t.Get(tag_unmanaged).int0);
            }

            {
                var t = tag_obj.New("123");
                Console.WriteLine(t.Get(tag_obj));
            }

            {
                var t = tag_managed.New(new ManagedStruct { int0 = 1, int1 = 2, str0 = "abc", str1 = "dce" });
                Console.WriteLine(t.Match(
                    tag_int, i => i.ToString(),
                    tag_obj, o => o?.ToString(),
                    tag_managed, m => m.str0 + m.str1,
                    () => "default"));
            }
        }

        #pragma warning disable CS0649
        public struct LargeManagedStruct
        {
            public object a0;
            public object a1;
            public object a2;
            public object a3;
        }
        #pragma warning restore CS0649

        internal static void GenericTags()
        {
            var tests = new MyConcreteTaggedUnion[]
            {
                1,
                "abc",
                // Implicit or explicit construction is supported.
                new ManagedStruct { int0 = 1, int1 = 2, str0 = "abc", str1 = "dce" },
                new(new UnmanagedStruct { int0 = 1, int1 = 2 }),
            };

            if (TagHelper<LargeManagedStruct>.CheckRefTypeSizeGood<TaggedUnion_3_15>())
                Console.WriteLine("LargeManagedStruct fits");
            else
                Console.WriteLine("LargeManagedStruct doesn't fit");

            foreach (var t in tests)
            {
                // Match with a return type
                Console.WriteLine("match return string:" + t.Match(
                    i => i.ToString(),
                    s => s,
                    m => m.str0 + m.str1,
                    u => u.int0.ToString()));

                // TryGet<V>
                {
                    if (t.TryGet(out int i))
                        Console.WriteLine("generic try: int " + i);
                    else if (t.TryGet(out string? s))
                        Console.WriteLine("generic try: string " + s);
                    else if (t.TryGet(out ManagedStruct m))
                        Console.WriteLine("generic try: managed " + m.str0 + m.str1);
                    else if (t.TryGet(out UnmanagedStruct u))
                        Console.WriteLine("generic try: unmanaged " + u.int0);
                    else
                        Debug.Fail("Should not happen");
                }

                // Action type
                t.Match(
                    i => Console.WriteLine("void match: int " + i),
                    s => Console.WriteLine("void match: string " + s),
                    m => Console.WriteLine("void match: managed " + m.str0 + m.str1),
                    u => Console.WriteLine("void match: unmanaged " + u.int0));

                // Match with a default case
                Console.WriteLine("match with default: " + t.Match(
                    i => i.ToString(),
                    defaultCase: () => "default"));

                // TryGetTypeIndex
                {
                    if (t.TryGet0(out var i))
                        Console.WriteLine("try by index: int " + i);
                    else if (t.TryGet1(out var s))
                        Console.WriteLine("try by index: string " + s);
                    else if (t.TryGet2(out var m))
                        Console.WriteLine("try by index: managed " + m.str0 + m.str1);
                    else if (t.TryGet3(out var u))
                        Console.WriteLine("try by index: unmanaged " + u.int0);
                    else
                        Debug.Fail("Should not happen");
                }

                Console.WriteLine();
            }
        }
    }
}