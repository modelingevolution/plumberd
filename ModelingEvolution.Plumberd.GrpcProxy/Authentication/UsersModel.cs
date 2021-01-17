using System;
using System.Collections.Concurrent;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.GrpcProxy.Authentication
{
    public class UsersModel
    {
        private readonly ConcurrentDictionary<Guid, UserInfo> _index;
        private readonly ConcurrentDictionary<string, UserInfo> _indexByEmail;
        /// <summary>
        /// o(1)
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public UserInfo FindByEmail(string email)
        {
            if (_indexByEmail.TryGetValue(email, out var u))
                return u;
            return null;
        }
        /// <summary>
        /// o(1)
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        public UserInfo FindByUserId(Guid userid)
        {
            if (_index.TryGetValue(userid, out var u))
                return u;
            return null;
        }

        public UsersModel()
        {
            _index = new ConcurrentDictionary<Guid, UserInfo>();
            _indexByEmail = new ConcurrentDictionary<string, UserInfo>();
        }

        public void Given(IMetadata m, AuthorizationDataRetrieved ev)
        {
            if (!_index.TryGetValue(m.StreamId(), out var u))
            {
                u = new UserInfo() { Email = ev.Email, Name=ev.Name, Id = m.StreamId()};
                _index.TryAdd(m.StreamId(), u);
                _indexByEmail.TryAdd(ev.Email, u);
            }
            else
            {
                u.Name = ev.Name;
                if (u.Email != ev.Email)
                {
                    _indexByEmail.TryRemove(u.Email, out var prv);
                    u.Email = ev.Email;
                    _indexByEmail.TryAdd(ev.Email, u);
                }
            }
        }
    }
}