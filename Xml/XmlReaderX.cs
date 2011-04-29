﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using NewLife.Reflection;
using NewLife.Serialization;

namespace NewLife.Xml
{
    /// <summary>
    /// Xml读取器
    /// </summary>
    public class XmlReaderX : ReaderBase<XmlSerialSettings>
    {
        #region 属性
        private XmlReader _Reader;
        /// <summary>读取器</summary>
        public XmlReader Reader
        {
            get
            {
                if (_Reader == null)
                {
                    XmlReaderSettings settings = new XmlReaderSettings();
                    settings.IgnoreWhitespace = true;
                    settings.IgnoreComments = true;
                    _Reader = XmlReader.Create(Stream, settings);
                }
                return _Reader;
            }
            set
            {
                _Reader = value;

                XmlTextReader xr = _Reader as XmlTextReader;
                if (xr != null && Settings.Encoding != xr.Encoding) Settings.Encoding = xr.Encoding;
            }
        }

        /// <summary>
        /// 数据流。更改数据流后，重置Reader为空，以使用新的数据流
        /// </summary>
        public override Stream Stream
        {
            get
            {
                return base.Stream;
            }
            set
            {
                if (base.Stream != value) _Reader = null;
                base.Stream = value;
            }
        }

        private String _RootName;
        /// <summary>根元素名</summary>
        public String RootName
        {
            get { return _RootName; }
            set { _RootName = value; }
        }
        #endregion

        #region 基础元数据
        #region 字节
        /// <summary>
        /// 从当前流中读取下一个字节，并使流的当前位置提升 1 个字节。
        /// </summary>
        /// <returns></returns>
        public override byte ReadByte() { return ReadBytes(1)[0]; }

        /// <summary>
        /// 从当前流中将 count 个字节读入字节数组，并使当前位置提升 count 个字节。
        /// </summary>
        /// <param name="count">要读取的字节数。</param>
        /// <returns></returns>
        public override byte[] ReadBytes(int count)
        {
            if (count <= 0) return null;

            Byte[] buffer = new Byte[count];
            Int32 n = Settings.MemberAsAttribute ? Reader.ReadContentAsBase64(buffer, 0, count) : Reader.ReadElementContentAsBase64(buffer, 0, count);

            if (n == count) return buffer;

            Byte[] data = new Byte[n];
            Buffer.BlockCopy(buffer, 0, data, 0, n);

            return data;
        }
        #endregion

        #region 有符号整数
        /// <summary>
        /// 从当前流中读取 2 字节有符号整数，并使流的当前位置提升 2 个字节。
        /// </summary>
        /// <returns></returns>
        public override short ReadInt16() { return (Int16)ReadInt32(); }

        /// <summary>
        /// 从当前流中读取 4 字节有符号整数，并使流的当前位置提升 4 个字节。
        /// </summary>
        /// <returns></returns>
        public override int ReadInt32() { return Settings.MemberAsAttribute ? Reader.ReadContentAsInt() : Reader.ReadElementContentAsInt(); }

        /// <summary>
        /// 从当前流中读取 8 字节有符号整数，并使流的当前位置向前移动 8 个字节。
        /// </summary>
        /// <returns></returns>
        public override long ReadInt64() { return Settings.MemberAsAttribute ? Reader.ReadContentAsLong() : Reader.ReadElementContentAsLong(); }
        #endregion

        #region 浮点数
        /// <summary>
        /// 从当前流中读取 4 字节浮点值，并使流的当前位置提升 4 个字节。
        /// </summary>
        /// <returns></returns>
        public override float ReadSingle() { return Settings.MemberAsAttribute ? Reader.ReadContentAsFloat() : Reader.ReadElementContentAsFloat(); }

        /// <summary>
        /// 从当前流中读取 8 字节浮点值，并使流的当前位置提升 8 个字节。
        /// </summary>
        /// <returns></returns>
        public override double ReadDouble() { return Settings.MemberAsAttribute ? Reader.ReadContentAsDouble() : Reader.ReadElementContentAsDouble(); }
        #endregion

        #region 字符串
        ///// <summary>
        ///// 从当前流中读取 count 个字符，以字符数组的形式返回数据，并根据所使用的 Encoding 和从流中读取的特定字符，提升当前位置。
        ///// </summary>
        ///// <param name="count">要读取的字符数。</param>
        ///// <returns></returns>
        //public override char[] ReadChars(int count)
        //{
        //    // count个字符可能的最大字节数
        //    Int32 max = Settings.Encoding.GetMaxByteCount(count);

        //    // 首先按最小值读取
        //    Byte[] data = ReadBytes(count);

        //    // 相同，最简单的一种
        //    if (max == count) return Settings.Encoding.GetChars(data);

        //    // 按最大值准备一个字节数组
        //    Byte[] buffer = new Byte[max];
        //    // 复制过去
        //    Buffer.BlockCopy(data, 0, buffer, 0, data.Length);

        //    // 遍历，以下算法性能较差，将来可以考虑优化
        //    Int32 i = 0;
        //    for (i = count; i < max; i++)
        //    {
        //        Int32 n = Settings.Encoding.GetCharCount(buffer, 0, i);
        //        if (n >= count) break;

        //        buffer[i] = ReadByte();
        //    }

        //    return Settings.Encoding.GetChars(buffer, 0, i);
        //}

        /// <summary>
        /// 从当前流中读取一个字符串。字符串有长度前缀，一次 7 位地被编码为整数。
        /// </summary>
        /// <returns></returns>
        public override string ReadString() { return Settings.MemberAsAttribute ? Reader.ReadContentAsString() : Reader.ReadElementContentAsString(); }
        #endregion

        #region 其它
        /// <summary>
        /// 从当前流中读取 Boolean 值，并使该流的当前位置提升 1 个字节。
        /// </summary>
        /// <returns></returns>
        public override bool ReadBoolean() { return Settings.MemberAsAttribute ? Reader.ReadContentAsBoolean() : Reader.ReadElementContentAsBoolean(); }

        /// <summary>
        /// 从当前流中读取十进制数值，并将该流的当前位置提升十六个字节。
        /// </summary>
        /// <returns></returns>
        public override decimal ReadDecimal() { return Settings.MemberAsAttribute ? Reader.ReadContentAsDecimal() : Reader.ReadElementContentAsDecimal(); }

        /// <summary>
        /// 读取一个时间日期
        /// </summary>
        /// <returns></returns>
        public override DateTime ReadDateTime() { return Settings.MemberAsAttribute ? Reader.ReadContentAsDateTime() : Reader.ReadElementContentAsDateTime(); }
        #endregion
        #endregion

        #region 字典
        protected override IEnumerable<DictionaryEntry> ReadDictionary(Type keyType, Type valueType, int count, ReadObjectCallback callback)
        {
            Reader.ReadStartElement();

            IEnumerable<DictionaryEntry> rs = base.ReadDictionary(keyType, valueType, count, callback);

            Reader.ReadEndElement();

            return rs;
        }

        /// <summary>
        /// 读取字典项
        /// </summary>
        /// <param name="keyType">键类型</param>
        /// <param name="valueType">值类型</param>
        /// <param name="value">字典项</param>
        /// <param name="index">元素序号</param>
        /// <param name="callback">处理成员的方法</param>
        /// <returns>是否读取成功</returns>
        public override bool ReadDictionaryEntry(Type keyType, Type valueType, ref DictionaryEntry value, Int32 index, ReadObjectCallback callback)
        {
            Object key = null;
            Object val = null;

            Reader.ReadStartElement();
            if (!ReadObject(keyType, ref key)) return false;
            Reader.ReadEndElement();

            Reader.ReadStartElement();
            if (!ReadObject(valueType, ref val)) return false;
            Reader.ReadEndElement();

            value.Key = key;
            value.Value = val;

            return true;
        }
        #endregion

        #region 枚举
        /// <summary>
        /// 读取元素集合
        /// </summary>
        /// <param name="type"></param>
        /// <param name="elementType"></param>
        /// <param name="count">元素个数</param>
        /// <param name="callback">处理元素的方法</param>
        /// <returns></returns>
        protected override IEnumerable ReadItems(Type type, Type elementType, Int32 count, ReadObjectCallback callback)
        {
            Reader.ReadStartElement();

            IEnumerable rs = base.ReadItems(type, elementType, count, callback);

            Reader.ReadEndElement();

            return rs;
        }

        /// <summary>
        /// 读取项
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <param name="index">元素序号</param>
        /// <param name="callback">处理元素的方法</param>
        /// <returns></returns>
        protected override bool ReadItem(Type type, ref object value, Int32 index, ReadObjectCallback callback)
        {
            if (Reader.Name != type.Name) return false;

            return base.ReadItem(type, ref value, index, callback);
        }
        #endregion

        #region 读取对象
        /// <summary>
        /// 尝试读取目标对象指定成员的值，通过委托方法递归处理成员
        /// </summary>
        /// <param name="type">要读取的对象类型</param>
        /// <param name="value">要读取的对象</param>
        /// <param name="callback">处理成员的方法</param>
        /// <returns>是否读取成功</returns>
        protected override bool OnReadObject(Type type, ref object value, ReadObjectCallback callback)
        {
            //Reader.Read();
            //Reader.ReadStartElement();

            while (Reader.NodeType != XmlNodeType.Element) { if (!Reader.Read())return false; }
            RootName = Reader.Name;

            //if (MemberStyle == XmlMemberStyle.Element) Reader.ReadStartElement();

            Boolean rs = base.OnReadObject(type, ref value, callback);

            //Reader.ReadEndElement();
            //Reader.Read();

            return rs;
        }

        /// <summary>
        /// 尝试读取目标对象指定成员的值，处理基础类型、特殊类型、基础类型数组、特殊类型数组，通过委托方法处理成员
        /// </summary>
        /// <param name="type">要读取的对象类型</param>
        /// <param name="value">要读取的对象</param>
        /// <param name="callback">处理成员的方法</param>
        /// <returns>是否读取成功</returns>
        public override Boolean ReadCustomObject(Type type, ref Object value, ReadObjectCallback callback)
        {
            // 如果是属性，使用基类就足够了
            if (Settings.MemberAsAttribute) return base.ReadCustomObject(type, ref value, callback);

            IObjectMemberInfo[] mis = GetMembers(type, value);
            if (mis == null || mis.Length < 1) return true;

            Dictionary<String, IObjectMemberInfo> dic = new Dictionary<string, IObjectMemberInfo>();
            foreach (IObjectMemberInfo item in mis)
            {
                if (!dic.ContainsKey(item.Name)) dic.Add(item.Name, item);
            }

            // 如果为空，实例化并赋值。
            if (value == null) value = TypeX.CreateInstance(type);

            Reader.ReadStartElement();
            //while (Reader.Read() && Reader.NodeType == XmlNodeType.Element)
            Int32 index = 0;
            while (Reader.NodeType == XmlNodeType.Element)
            {
                //Reader.ReadStartElement();
                if (Reader.IsEmptyElement)
                {
                    Reader.Read();
                    continue;
                }

                if (!dic.ContainsKey(Reader.Name))
                {
                    Reader.ReadEndElement();
                    continue;
                }

                Depth++;
                IObjectMemberInfo member = dic[Reader.Name];
                Debug("ReadMember", member.Name, member.Type.Name);

                if (!ReadMember(ref value, member, index++, callback)) return false;
                Depth--;

                //Reader.ReadEndElement();
            }
            //临时办法，将来解决
            if (Reader.NodeType != XmlNodeType.None)
                Reader.ReadEndElement();

            return true;
        }

        /// <summary>
        /// 读取成员
        /// </summary>
        /// <param name="value">要读取的对象</param>
        /// <param name="member">成员</param>
        /// <param name="index">成员索引</param>
        /// <param name="callback">处理成员的方法</param>
        /// <returns>是否读取成功</returns>
        protected override bool OnReadMember(ref object value, IObjectMemberInfo member, Int32 index, ReadObjectCallback callback)
        {
            if (Settings.MemberAsAttribute)
            {
                Reader.MoveToAttribute(member.Name);
            }

            return base.OnReadMember(ref value, member, index, callback);
        }
        #endregion
    }
}