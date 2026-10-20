using System;

namespace Runtime.Dialogue.Core
{
    /// <summary>
    /// コマンドや分岐ハンドラーの説明・使い方を記述する属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class HandlerInfoAttribute : Attribute
    {
        public string Description { get; }
        public string Usage { get; }

        public HandlerInfoAttribute(string description, string usage = "")
        {
            Description = description;
            Usage = usage;
        }
    }
}