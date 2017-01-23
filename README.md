# FluorineFX
Пакет расширений для FluorineFX.


## FluorineFX.Serialization
### Пример работы:
```!CSharp
    // Описываем модель данных.
    [AmfObject("namespase.of.your.object")]
    public class CustomAmfObject
    {
        [AmfMember("bit_prop")]
        public bool BooleanProperty { get; set; } = true;
        
        [AmfMember]
        public sbyte UnsignedByteProperty { get; set; } = 2;
        
        public string StringProperty { get; set; } = "test";

        [AmfMember("bit_fld")] public bool booleanField = false;
        [AmfMember] public float singleField = -5.00065f;
        public string stringField = "test2";

        public CustomAmfObject() { }
    }
    
    using (MemoryStream memoryStream = new MemoryStream())
    using (AMFWriter amfWriter = new AMFWriter(memoryStream))
    using (WebClient client = new WebClient())
    {
        CustomAmfObject customObject = new CustomAmfObject();
        
        byte[] serializedBuffer = customObject.SerializeToAmf();    // Сериализуем модель данных.
        
        amfWriter.WriteBytes(serializedBuffer);
        client.Headers[HttpRequestHeader.ContentType] = "application/x-amf";
        client.UploadData(Host, "POST", memoryStream.ToArray());
        
        CustomAmfObject deserializedObject = serializedBuffer.DeserializeFromAmf<CustomAmfObject>();    // Десериализуем буфер в объект.
    }
