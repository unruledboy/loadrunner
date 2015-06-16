using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Org.LoadRunner.Core.Infrastructure
{
    public class ConcurrentEnumerator<T> : IEnumerator<T>
    {
        #region Fields
        private readonly IEnumerator<T> _inner;
        private readonly ReaderWriterLockSlim _lock;
        #endregion

        #region Constructor
        public ConcurrentEnumerator(IEnumerable<T> inner, ReaderWriterLockSlim @lock)
        {
            _lock = @lock;
            _lock.EnterReadLock();
            _inner = inner.GetEnumerator();
        }
        #endregion

        #region Methods
        public bool MoveNext()
        {
            return _inner.MoveNext();
        }

        public void Reset()
        {
            _inner.Reset();
        }

        public void Dispose()
        {
            _lock.ExitReadLock();
        }
        #endregion

        #region Properties
        public T Current
        {
            get { return _inner.Current; }
        }

        object IEnumerator.Current
        {
            get { return _inner.Current; }
        }
        #endregion
    }
}
