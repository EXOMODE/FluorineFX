using System;

namespace FluorineFX.Serialization
{
    /// <summary>
    /// Представляет атрибут сериализации экземпляра объекта AMF.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class AmfObjectAttribute : Attribute
    {
        /// <summary>
        /// Имя типа объекта.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="AmfObjectAttribute"/>.
        /// </summary>
        /// <param name="name">Имя типа объекта.</param>
        public AmfObjectAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="AmfObjectAttribute"/>.
        /// </summary>
        public AmfObjectAttribute() : this(null) { }
    }
}