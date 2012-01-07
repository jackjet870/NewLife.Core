﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace NewLife.Collections
{
    /// <summary>先进先出LIFO的原子栈结构，采用CAS保证线程安全。利用单链表实现。</summary>
    /// <remarks>
    /// 注意：<see cref="Push"/>、<see cref="TryPop"/>、<see cref="Pop"/>、<see cref="TryPeek"/>、<see cref="Peek"/>是重量级线程安全代码，不要随意更改。
    /// 
    /// 经过测试，对象数量在万级以上时，性能急剧下降！
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    [DebuggerDisplay("Count = {Count}")]
    public class InterlockedStack<T> : DisposeBase, IStack<T>, IEnumerable<T>, ICollection, IEnumerable
    {
        #region 字段
        /// <summary>栈顶</summary>
        private SingleListNode<T> Top;

        ///// <summary>版本</summary>
        //private Int32 _version;
        #endregion

        #region 属性
        private Int32 _Count;
        /// <summary>元素个数</summary>
        public Int32 Count { get { return _Count; } }
        #endregion

        #region 构造
        /// <summary>子类重载实现资源释放逻辑时必须首先调用基类方法</summary>
        /// <param name="disposing">从Dispose调用（释放所有资源）还是析构函数调用（释放非托管资源）</param>
        protected override void OnDispose(bool disposing)
        {
            base.OnDispose(disposing);

            Clear();
            Top = null;
        }
        #endregion

        #region 核心方法
        //private Int32 maxTimes = 0;
        /// <summary>向栈压入一个对象</summary>
        /// <remarks>重点解决多线程环境下资源争夺以及使用lock造成性能损失的问题</remarks>
        /// <param name="item"></param>
        public void Push(T item)
        {
            SingleListNode<T> newTop = new SingleListNode<T>(item);
            SingleListNode<T> oldTop;
            //Int32 times = 0;
            do
            {
                //times++;
                // 记住当前栈顶
                oldTop = Top;

                // 设置新对象的下一个节点为当前栈顶
                newTop.Next = oldTop;
            }
            // 比较并交换
            // 如果当前栈顶第一个参数的Top等于第三个参数，表明没有被别的线程修改，保存第二参数到第一参数中
            // 否则，不相等表明当前栈顶已经被修改过，操作失败，执行循环
            while (Interlocked.CompareExchange<SingleListNode<T>>(ref Top, newTop, oldTop) != oldTop);

            //if (times > 1) XTrace.WriteLine("命中次数：{0}", times);
            //if (times > maxTimes)
            //{
            //    maxTimes = times;
            //    XTrace.WriteLine("新命中次数：{0}", times);
            //}
            Interlocked.Increment(ref _Count);
            //Interlocked.Increment(ref _version);
        }

        /// <summary>从栈中弹出一个对象</summary>
        /// <returns></returns>
        public T Pop()
        {
            T item;
            if (!TryPop(out item)) throw new InvalidOperationException("栈为空！");

            return item;
        }

        /// <summary>尝试从栈中弹出一个对象</summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public Boolean TryPop(out T item)
        {
            SingleListNode<T> newTop;
            SingleListNode<T> oldTop;
            //Int32 times = 0;
            do
            {
                //times++;
                // 记住当前栈顶
                oldTop = Top;
                if (oldTop == null)
                {
                    item = default(T);
                    return false;
                }

                // 设置新栈顶为当前栈顶的下一个节点
                newTop = oldTop.Next;
            }
            // 比较并交换
            // 如果当前栈顶第一个参数的Top等于第三个参数，表明没有被别的线程修改，保存第二参数到第一参数中
            // 否则，不相等表明当前栈顶已经被修改过，操作失败，执行循环
            while (Interlocked.CompareExchange<SingleListNode<T>>(ref Top, newTop, oldTop) != oldTop);

            //if (times > 1) XTrace.WriteLine("命中次数：{0}", times);
            //if (times > maxTimes)
            //{
            //    maxTimes = times;
            //    XTrace.WriteLine("新命中次数：{0}", times);
            //}
            Interlocked.Decrement(ref _Count);
            //Interlocked.Increment(ref _version);

            item = oldTop.Item;
            // 断开关系链，避免内存泄漏
            oldTop.Next = null;
            oldTop.Item = default(T);

            return true;
        }

        /// <summary>获取栈顶对象，不弹栈</summary>
        /// <returns></returns>
        public T Peek()
        {
            T item;
            if (!TryPeek(out item)) throw new InvalidOperationException("栈为空！");

            return item;
        }

        /// <summary>尝试获取栈顶对象，不弹栈</summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public Boolean TryPeek(out T item)
        {
            var top = Top;
            if (top == null)
            {
                item = default(T);
                return false;
            }
            item = top.Item;
            return true;
        }
        #endregion

        #region 集合方法
        /// <summary>清空</summary>
        public void Clear()
        {
            var top = Top;
            _Count = 0;
            Top = null;

            for (var node = top; node != null; )
            {
                top = node;
                node = node.Next;

                // 断开关系链，避免内存泄漏
                top.Next = null;
                top.Item = default(T);
            }

            //Interlocked.Increment(ref _version);
        }

        /// <summary>转为数组</summary>
        /// <returns></returns>
        public T[] ToArray()
        {
            if (Count < 1) return null;

            T[] arr = new T[Count];
            ((ICollection)this).CopyTo(arr, 0);
            return arr;
        }
        #endregion

        #region ICollection 成员
        void ICollection.CopyTo(Array array, int index)
        {
            if (Top == null || array == null || index >= array.Length) return;

            for (var node = Top; node != null && index < array.Length; node = node.Next) array.SetValue(node.Item, index++);
        }

        bool ICollection.IsSynchronized { get { return true; } }

        private Object _syncRoot;
        object ICollection.SyncRoot
        {
            get
            {
                if (_syncRoot == null)
                {
                    Interlocked.CompareExchange(ref this._syncRoot, new object(), null);
                }
                return _syncRoot;
            }
        }
        #endregion

        #region IEnumerable 成员
        /// <summary>获取枚举器</summary>
        /// <returns></returns>
        public IEnumerator<T> GetEnumerator()
        {
            for (var node = Top; node != null; node = node.Next) yield return node.Item;
        }

        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
        #endregion
    }
}