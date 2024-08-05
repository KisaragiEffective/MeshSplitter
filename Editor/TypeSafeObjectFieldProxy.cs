using UnityEditor.UIElements;
using Object = UnityEngine.Object;

namespace KiriMeshSplitter
{
    internal class TypeSafeObjectField<T> where T: Object
    {
        internal T Value
        {
            get => ObjectField.value as T;
            set => ObjectField.value = value;
        }

        internal readonly ObjectField ObjectField;

        internal TypeSafeObjectField(ObjectField objectField)
        {
            this.ObjectField = objectField;
            ObjectField.objectType = typeof(T);
        }
    }
}
