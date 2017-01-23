using System;

namespace FluorineFX.Serialization
{
    /// <summary>
    /// Представляет атрибут для сериализации полей и свойств экземпляра объекта AMF.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class AmfMemberAttribute : Attribute
    {
        /// <summary>
        /// Имя свойства или поля.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="AmfMemberAttribute"/>.
        /// </summary>
        /// <param name="name">Имя свойства или поля.</param>
        public AmfMemberAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="AmfMemberAttribute"/>.
        /// </summary>
        public AmfMemberAttribute() : this(null) { }
    }
}