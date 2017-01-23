using FluorineFx.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace FluorineFX.Serialization
{
    /// <summary>
    /// Представляет набор расширений для работы с сериализацией/десериализацией объектов AMF.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Билдер для модуля динамической сборки.
        /// </summary>
        private static ModuleBuilder moduleBuilder;

        /// <summary>
        /// Статический конструктор класса <see cref="Extensions"/>.
        /// </summary>
        static Extensions()
        {
            AssemblyName assemblyName = new AssemblyName("AmfDynamicAssembly");    // Создаём новую среду выполнения кода.
            AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);    // Определяем среду выполнения.
            moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name, assemblyName.Name + ".dll");   // Определяем новый модуль для среды выполнения.
        }

        /// <summary>
        /// Находит и возвращает атрибут.
        /// </summary>
        /// <typeparam name="T">Тип искомого атрибута.</typeparam>
        /// <param name="sourceType">Исходный тип для поиска.</param>
        /// <returns></returns>
        private static T GetAttribute<T>(this Type sourceType) where T : Attribute
        {
            object[] attributes = sourceType.GetCustomAttributes(typeof(T), true);  // Получаем текущий атрибут.

            if (attributes == null || attributes.Length == 0) return default(T); // Если у типа объекта не задан атрибут - возвращаем null.

            return attributes[0] as T;
        }

        /// <summary>
        /// Находит и возвращает атрибут.
        /// </summary>
        /// <typeparam name="T">Тип искомого атрибута.</typeparam>
        /// <param name="sourceType">Исходный тип для поиска.</param>
        /// <returns></returns>
        private static T GetAttribute<T>(this MemberInfo sourceMember) where T : Attribute
        {
            object[] attributes = sourceMember.GetCustomAttributes(typeof(T), true);  // Получаем текущий атрибут.

            if (attributes == null || attributes.Length == 0) return default(T); // Если у типа объекта не задан атрибут - возвращаем null.

            return attributes[0] as T;
        }

        private static bool IsDefinedAttribute<T>(this Type sourceType)
        {
            object[] attributes = sourceType.GetCustomAttributes(typeof(T), true);  // Получаем текущий атрибут.
            return attributes != null && attributes.Length > 0;
        }

        /// <summary>
        /// Генерирует объект с метаданными типа в соответствии с заданными атрибутами <see cref="AmfObjectAttribute"/>, с полями типа, заданного в атрибутах <see cref="AmfMemberAttribute"/>.
        /// </summary>
        /// <param name="sourceObject">Исходный экземпляр объекта.</param>
        /// <returns></returns>
        private static object GenerateType(object sourceObject)
        {
            Type sourceType = sourceObject.GetType(); // Получаем метаданные типа исходного объекта.

            if (!sourceType.IsDefinedAttribute<AmfObjectAttribute>()) return sourceObject; // Если у типа объекта не задан атрибут - возвращаем как есть.

            string typeName = sourceType.GetAttribute<AmfObjectAttribute>().Name ?? sourceType.FullName;    // Определяем имя у типа.
            Type definedType = moduleBuilder.GetType(typeName);   // Пытаемся найти уже определенный в сборке тип.
            TypeBuilder typeBuilder = null; // Определяем билдер для нашего типа.

            Dictionary<string, object> properties = new Dictionary<string, object>();   // Словарь свойств объекта.
            Dictionary<string, object> fields = new Dictionary<string, object>();   // Словарь полей объекта.

            // Если тип в сборке еще не определен...
            if (definedType == null)
            {
                typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public);  // Опледеляем тип с нашим именем.

                ConstructorBuilder ctor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);  // Определяем конструктор.
                ILGenerator ctorIL = ctor.GetILGenerator();   // Получаем ссылку на генератор MSIL-инструкций для конструктора.
                ctorIL.Emit(OpCodes.Ldarg_0);  // Помещаем в стек вычислений нулевой аргумент. 
                ctorIL.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)); // Вызываем базовый конструктор для инициализации значения по умолчанию у нулевого аргумента.
                ctorIL.Emit(OpCodes.Ret);  // Помещаем в стек вычислений инструкцию о возврате из метода.

                // Перебираем все свойства нашего типа.
                foreach (PropertyInfo propertyInfo in sourceType.GetProperties())
                {
                    AmfMemberAttribute attribute = propertyInfo.GetAttribute<AmfMemberAttribute>();  // Получаем наш кастомный атрибут типа AmfMemberAttribute.

                    if (attribute == null) continue; // Если атрибут не указан - пропускаем свойство.

                    string propertyName = attribute.Name ?? propertyInfo.Name; // Получаем имя свойства.
                    object propertyValue = propertyInfo.GetValue(sourceObject, null); // Получаем значение свойства.

                    AmfObjectAttribute propertyAttribute = propertyInfo.PropertyType.GetAttribute<AmfObjectAttribute>();  // Получаем атрибут у свойства.

                    Type propertyType = propertyInfo.PropertyType;  // Получаем метаданные типа свойства.

                    // Если у типа задан атрибут...
                    if (propertyAttribute != null)
                    {
                        propertyValue = GenerateType(propertyValue); // Генерируем объект типа, заданного в атрибуте.
                        propertyType = propertyValue.GetType();   // Обновляем тип свойства.
                    }

                    FieldBuilder fieldBuilder = typeBuilder.DefineField($"m_{propertyName}", propertyType, FieldAttributes.Private);   // Определяем новое приватное поле.

                    PropertyBuilder propertyBuilder = typeBuilder.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null); // Определяем новое свойство.
                    MethodAttributes getSetAttr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;  // Устанавливаем атрибуты аксессору и мутатору свойства.

                    MethodBuilder methodBuilderAccessor = typeBuilder.DefineMethod($"get_{propertyName}", getSetAttr, propertyType, Type.EmptyTypes);  // Определяем аксессор.
                    ILGenerator accessorIL = methodBuilderAccessor.GetILGenerator();   // Получаем ссылку на генератор MSIL-инструкций для аксессора.
                    accessorIL.Emit(OpCodes.Ldarg_0);     // Помещаем в стек вычислений нулевой аргумент. 
                    accessorIL.Emit(OpCodes.Ldfld, fieldBuilder);   // Помещаем в стек вычислений инструкцию о получении значения по ссылке поля.
                    accessorIL.Emit(OpCodes.Ret);     // Помещаем в стек вычислений инструкцию о возврате из метода.
                    MethodBuilder methodBuilderSetter = typeBuilder.DefineMethod($"set_{propertyName}", getSetAttr, null, new Type[] { propertyType });    // Определяем мутатор.
                    ILGenerator setterIL = methodBuilderSetter.GetILGenerator();    // Получаем ссылку на генератор MSIL-инструкций для мутатора.
                    setterIL.Emit(OpCodes.Ldarg_0);   // Помещаем в стек вычислений нулевой аргумент.
                    setterIL.Emit(OpCodes.Ldarg_1); // Помещаем в стек вычислений первый аргумент.
                    setterIL.Emit(OpCodes.Stfld, fieldBuilder); // Помещаем в стек вычислений инструкцию о сохранении значения по ссылке поля.
                    setterIL.Emit(OpCodes.Ret);   // Помещаем в стек вычислений инструкцию о возврате из метода.

                    propertyBuilder.SetGetMethod(methodBuilderAccessor);    // Добавляем свойству аксессор.
                    propertyBuilder.SetSetMethod(methodBuilderSetter);  // Добавляем свойству мутатор.

                    properties.Add(propertyName, propertyValue);  // Сохраняем значения в словарь для дальнейшей передачи свойствам значений.
                }

                // Перебираем все поля нашего типа.
                foreach (FieldInfo fieldInfo in sourceType.GetFields())
                {
                    AmfMemberAttribute attribute = fieldInfo.GetAttribute<AmfMemberAttribute>();  // Получаем наш кастомный атрибут типа AmfMemberAttribute.

                    if (attribute == null) continue; // Если атрибут не указан - пропускаем поле.

                    string fieldName = attribute.Name ?? fieldInfo.Name; // Получаем имя поля.
                    object fieldValue = fieldInfo.GetValue(sourceObject); // Получаем значение поля.
                    Type fieldType = fieldInfo.FieldType;  // Получаем метаданные типа поля.

                    AmfObjectAttribute fieldAttribute = fieldInfo.FieldType.GetAttribute<AmfObjectAttribute>();  // Получаем атрибут у поля.

                    // Если у типа задан атрибут...
                    if (fieldAttribute != null)
                    {
                        fieldValue = GenerateType(fieldValue); // Генерируем объект типа, заданного в атрибуте.
                        fieldType = fieldValue.GetType();   // Обновляем тип поля.
                    }

                    FieldBuilder fieldBuilder = typeBuilder.DefineField(fieldName, fieldType, FieldAttributes.Public);   // Определяем новое поле.

                    fields.Add(fieldName, fieldValue);  // Сохраняем значения в словарь для дальнейшей передачи свойствам значений.
                }
            }
            else
            {
                // Перебираем все свойства нашего типа.
                foreach (PropertyInfo propertyInfo in sourceType.GetProperties())
                {
                    AmfMemberAttribute attribute = propertyInfo.GetAttribute<AmfMemberAttribute>();  // Получаем наш кастомный атрибут типа AmfMemberAttribute.

                    if (attribute == null) continue; // Если атрибут не указан - пропускаем свойство.

                    string propertyName = attribute.Name ?? propertyInfo.Name; // Получаем имя свойства.
                    object propertyValue = propertyInfo.GetValue(sourceObject, null); // Получаем значение свойства.

                    AmfObjectAttribute propertyAttribute = propertyInfo.PropertyType.GetAttribute<AmfObjectAttribute>();  // Получаем атрибут у свойства.

                    Type propertyType = propertyInfo.PropertyType;  // Получаем метаданные типа свойства.

                    // Если у типа задан атрибут...
                    if (propertyAttribute != null)
                    {
                        propertyValue = GenerateType(propertyValue); // Генерируем объект типа, заданного в атрибуте.
                        propertyType = propertyValue.GetType();   // Обновляем тип свойства.
                    }

                    properties.Add(propertyName, propertyValue);  // Сохраняем значения в словарь для дальнейшей передачи свойствам значений.
                }

                // Перебираем все поля нашего типа.
                foreach (FieldInfo fieldInfo in sourceType.GetFields())
                {
                    AmfMemberAttribute attribute = fieldInfo.GetAttribute<AmfMemberAttribute>();  // Получаем наш кастомный атрибут типа AmfMemberAttribute.

                    if (attribute == null) continue; // Если атрибут не указан - пропускаем поле.

                    string fieldName = attribute.Name ?? fieldInfo.Name; // Получаем имя поля.
                    object fieldValue = fieldInfo.GetValue(sourceObject); // Получаем значение поля.
                    Type fieldType = fieldInfo.FieldType;  // Получаем метаданные типа поля.

                    AmfObjectAttribute fieldAttribute = fieldInfo.FieldType.GetAttribute<AmfObjectAttribute>();  // Получаем атрибут у поля.

                    // Если у типа задан атрибут...
                    if (fieldAttribute != null)
                    {
                        fieldValue = GenerateType(fieldValue); // Генерируем объект типа, заданного в атрибуте.
                        fieldType = fieldValue.GetType();   // Обновляем тип поля.
                    }

                    fields.Add(fieldName, fieldValue);  // Сохраняем значения в словарь для дальнейшей передачи свойствам значений.
                }
            }

            object targetObject = Activator.CreateInstance(definedType ?? typeBuilder.CreateType() );  // Создаём инстанс нашего динамического типа.

            // Раставляем значения всем свойствам объекта.
            foreach (KeyValuePair<string, object> property in properties) targetObject.GetType().GetProperty(property.Key).SetValue(targetObject, property.Value, null);

            // Раставляем значения всем полям объекта.
            foreach (KeyValuePair<string, object> field in fields) targetObject.GetType().GetField(field.Key).SetValue(targetObject, field.Value);

            return targetObject;
        }

        /// <summary>
        /// Генерирует массив объектов с метаданными типа в соответствии с заданными атрибутами <see cref="AmfObjectAttribute"/>, с полями типа, заданного в атрибутах <see cref="AmfMemberAttribute"/>.
        /// </summary>
        /// <param name="sourceObject">Массив исходных объектов.</param>
        /// <returns></returns>
        private static object[] GenerateType(object[] sourceObjects)
        {
            for (int i = 0; i < sourceObjects.Length; i++) sourceObjects[i] = GenerateType(sourceObjects[i]);   // Генерируем типы для каждого элемента массива.
            return sourceObjects;
        }

        /// <summary>
        /// Сериализует объект в буффер AMF.
        /// </summary>
        /// <param name="sourceObject">Исходный объект.</param>
        /// <param name="version">Версия AMF.</param>
        /// <returns></returns>
        public static byte[] SerializeToAmf(this object sourceObject, ushort version)
        {
            using (MemoryStream memoryStream = new MemoryStream())  // Открываем поток для записи данных в буфер.
            using (AMFSerializer amfSerializer = new AMFSerializer(memoryStream))   // Инициализируем сериализатор для AMF.
            {
                AMFMessage amfMessage = new AMFMessage(version);  // Создаём сообщение для передачи серверу с заданным номером версии AMF.
                AMFBody amfBody = new AMFBody(AMFBody.OnResult, null, GenerateType(sourceObject));    // Создаём тело для сообщения AMF.

                amfMessage.AddBody(amfBody);    // Добавляем body для сообщения AMF.
                amfSerializer.WriteMessage(amfMessage); // Сериализуем сообщение.

                return memoryStream.ToArray();  // Преобразовывает поток памяти в буфер и возвращает.
            }
        }

        /// <summary>
        /// Сериализует объект в буффер AMF3.
        /// </summary>
        /// <param name="sourceObject">Исходный объект.</param>
        /// <returns></returns>
        public static byte[] SerializeToAmf(this object sourceObject) => sourceObject.SerializeToAmf(3);

        /// <summary>
        /// Сериализует объект в файл *.amf.
        /// </summary>
        /// <param name="sourceObject">Сериализуемый объект.</param>
        /// <param name="path">Путь сохранения.</param>
        /// <param name="version">Номер версии AMF.</param>
        public static void SerializeToAmf(this object sourceObject, string path, ushort version)
            => File.WriteAllBytes($"{path}.amf", sourceObject.SerializeToAmf(version));

        /// <summary>
        /// Сериализует объект в файл *.amf. Версия AMF равна 3.
        /// </summary>
        /// <param name="sourceObject">Сериализуемый объект.</param>
        /// <param name="path">Путь сохранения.</param>
        public static void SerializeToAmf(this object sourceObject, string path) => sourceObject.SerializeToAmf(path, 3);

        /// <summary>
        /// Десериализует буфер данных в объект AMF.
        /// </summary>
        /// <typeparam name="T">Тип десериализуемого объекта.</typeparam>
        /// <param name="sourceBuffer">Исходный буфер данных объекта.</param>
        /// <returns></returns>
        public static T DeserializeFromAmf<T>(this byte[] sourceBuffer) where T : class
        {
            using (MemoryStream memoryStream = new MemoryStream(sourceBuffer))  // Открываем поток для чтения данных из буфера.
            using (AMFDeserializer amfDeserializer = new AMFDeserializer(memoryStream))      // Инициализируем десериализатор для AMF.
            {
                AMFMessage amfMessage = amfDeserializer.ReadAMFMessage();   // Получем сообщение AMF.
                AMFBody amfBody = amfMessage.GetBodyAt(0);  // Получаем body из сообщения AMF.

                object amfObject = amfBody.Content; // Получаем объект из body AMF.
                Type amfObjectType = amfObject.GetType();   // Получаем метаданные типа объекта AMF.

                // Формируем запрос на получение всей коллекции нужных нам типов с заданными атрибутами.
                IEnumerable<Type> types = from type in Assembly.GetExecutingAssembly().GetTypes()
                                          where Attribute.IsDefined(type, typeof(AmfObjectAttribute))
                                          select type;

                Type currentType = null;   // Определяем текущий тип объекта из нашей сборки.

                // Проходим по всем найденным типам с нашим атрибутом.
                foreach (Type type in types)
                {
                    AmfObjectAttribute attribute = type.GetAttribute<AmfObjectAttribute>();   // Получаем наш атрибут.

                    if (attribute == null || attribute.Name != amfObjectType.FullName) continue;   // Если в атрибуте задано другое имя - пропускаем итерацию.

                    currentType = type; // Иначе сохраняем текущий тип объекта.
                    break;
                }

                if (currentType == null) return default(T); // Если тип не найден - возвращаем null.

                object targetObject = Activator.CreateInstance(currentType);  // Создаём инстанс нашего типа.

                // Анализируем все свойства нашего класса.
                foreach (PropertyInfo propertyInfo in currentType.GetProperties())
                {
                    AmfMemberAttribute attribute = propertyInfo.GetAttribute<AmfMemberAttribute>();   // Получаем наш кастомный атрибут.

                    if (attribute == null) continue;    // Если атрибут не задан - пропускаем.

                    propertyInfo.SetValue(targetObject, amfObjectType.GetProperty(attribute.Name).GetValue(amfObject, null), null);   // Получаем значение свойства у десериализуемого объекта и сохраняем его в свойстве нашего объекта.
                }

                // Анализируем все поля нашего класса.
                foreach (FieldInfo fieldInfo in currentType.GetFields())
                {
                    AmfMemberAttribute attribute = fieldInfo.GetAttribute<AmfMemberAttribute>();   // Получаем наш кастомный атрибут.

                    if (attribute == null) continue;    // Если атрибут не задан - пропускаем.

                    fieldInfo.SetValue(targetObject, amfObjectType.GetField(attribute.Name).GetValue(amfObject));   // Получаем значение поля у десериализуемого объекта и сохраняем его в поле нашего объекта.
                }

                return targetObject as T;  // Приводит к типу T и возвращает текущий объект.
            }
        }

        /// <summary>
        /// Десериализует объект из файла *.amf.
        /// </summary>
        /// <typeparam name="T">Тип десериализуемого объекта.</typeparam>
        /// <param name="obj">Десериализуемый объект.</param>
        /// <param name="path">Путь к файлу объекта.</param>
        /// <returns>Десериализованный объект AMF.</returns>
        public static T DeserializeFromAmf<T>(this object obj, string path) where T : class => File.ReadAllBytes($"{path}.amf").DeserializeFromAmf<T>();
    }
}