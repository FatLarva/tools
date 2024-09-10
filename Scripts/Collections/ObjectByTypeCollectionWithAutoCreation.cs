using System;
using System.Collections;
using System.Collections.Generic;

namespace Tools.Collections
{
    public class ObjectByTypeCollectionWithAutoCreation<TBaseType> : IEnumerable<TBaseType>
        where TBaseType : class
    {
        private readonly ObjectByTypeCollection<TBaseType> _actualCollection;

        public ObjectByTypeCollectionWithAutoCreation(int initialMapSize = 4)
        {
            _actualCollection = new ObjectByTypeCollection<TBaseType>(initialMapSize);
        }

        public void AddObject(TBaseType newObject)
        {
            _actualCollection.AddObject(newObject);
        }

        public void RemoveByType(Type typeToRemove)
        {
            _actualCollection.RemoveByType(typeToRemove);
        }

        public void RemoveObject(TBaseType objectToRemove)
        {
            _actualCollection.RemoveObject(objectToRemove);
        }

        public bool ContainsObjectOfType<TSpecificType>()
            where TSpecificType : class, TBaseType
        {
            return _actualCollection.ContainsObjectOfType<TSpecificType>();
        }

        public bool ContainsObject(TBaseType objectToCheck)
        {
            return _actualCollection.ContainsObject(objectToCheck);
        }

        public TSpecificType GetObjectAs<TSpecificType>()
            where TSpecificType : class, TBaseType, new()
        {
            if (!_actualCollection.TryGetObjectAs(out TSpecificType desiredObject))
            {
                desiredObject = new TSpecificType();
                _actualCollection.AddObject(desiredObject);
            }

            return desiredObject;
        }

        public bool TryGetObjectAs<TSpecificType>(out TSpecificType specificDesiredObject)
            where TSpecificType : class, TBaseType, new()
        {
            if (!_actualCollection.TryGetObjectAs(out TSpecificType desiredObject))
            {
                desiredObject = new TSpecificType();
                _actualCollection.AddObject(desiredObject);
            }

            specificDesiredObject = desiredObject;

            return true;
        }
        
        public void Clear()
        {
            _actualCollection.Clear();
        }
        
        IEnumerator<TBaseType> IEnumerable<TBaseType>.GetEnumerator()
        {
            return (_actualCollection as IEnumerable<TBaseType>).GetEnumerator();
        }


        IEnumerator IEnumerable.GetEnumerator()
        {
            return (_actualCollection as IEnumerable<TBaseType>).GetEnumerator();
        }
    }
}
