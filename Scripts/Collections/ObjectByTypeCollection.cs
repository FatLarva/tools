using System;
using System.Collections;
using System.Collections.Generic;

namespace Tools.Collections
{
    public class ObjectByTypeCollection<TBaseType> : IEnumerable<TBaseType>
        where TBaseType : class
    {
        private readonly Dictionary<Type, TBaseType> _objectByTypeMap;

        public ObjectByTypeCollection(int initialMapSize = 4)
        {
            _objectByTypeMap = new Dictionary<Type, TBaseType>(initialMapSize);
        }

        public void AddObject(TBaseType newObject)
        {
            _objectByTypeMap.Add(newObject.GetType(), newObject);
        }

        public void RemoveByType(Type typeToRemove)
        {
            _objectByTypeMap.Remove(typeToRemove);
        }

        public void RemoveObject(TBaseType objectToRemove)
        {
            _objectByTypeMap.Remove(objectToRemove.GetType());
        }

        public TSpecificType GetObjectAs<TSpecificType>()
            where TSpecificType : class, TBaseType
        {
            if (!_objectByTypeMap.TryGetValue(typeof(TSpecificType), out TBaseType desiredObject))
            {
                throw new ArgumentException($"No object of type {typeof(TSpecificType)} in this collection.");
            }

            return (TSpecificType)desiredObject;
        }
        
        public bool TryGetObjectAs<TSpecificType>(out TSpecificType specificDesiredObject)
            where TSpecificType : class, TBaseType
        {
            if (_objectByTypeMap.TryGetValue(typeof(TSpecificType), out TBaseType desiredObject))
            {
                specificDesiredObject = (TSpecificType)desiredObject;

                return true;
            }

            specificDesiredObject = default;

            return false;
        }
        
        public TBaseType GetObject(Type type)
        {
            if (!_objectByTypeMap.TryGetValue(type, out TBaseType desiredObject))
            {
                throw new ArgumentException($"There is no object of type {type} in this collection.");
            }

            return desiredObject;
        }
        
        public bool TryGetObject(Type type, out TBaseType specificDesiredObject)
        {
            if (_objectByTypeMap.TryGetValue(type, out TBaseType desiredObject))
            {
                specificDesiredObject = desiredObject;

                return true;
            }

            specificDesiredObject = default;

            return false;
        }

        public bool ContainsObjectOfType<TSpecificType>()
            where TSpecificType : class, TBaseType
        {
            return _objectByTypeMap.ContainsKey(typeof(TSpecificType));
        }
        
        public bool ContainsObjectOfType(Type type)
        {
            return _objectByTypeMap.ContainsKey(type);
        }

        public bool ContainsObject(TBaseType objectToCheck)
        {
            return _objectByTypeMap.ContainsValue(objectToCheck);
        }
        
        public void Clear()
        {
            _objectByTypeMap.Clear();
        }

        IEnumerator<TBaseType> IEnumerable<TBaseType>.GetEnumerator()
        {
            return _objectByTypeMap.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (this as IEnumerable<TBaseType>).GetEnumerator();
        }
    }
}
