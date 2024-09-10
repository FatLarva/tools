using System;
using System.Collections.Generic;

namespace Tools.UsageTokens
{
    public abstract class UsageTokenProvider<TToken> 
        where TToken : struct, IUsageToken
    {
        private readonly List<TToken> _tokensInUse = new(5);
        
        private int _nextTokenId;

        public bool HasUnredeemedTokens => _tokensInUse.Count > 0;
        
        public TToken GetToken()
        {
            var token = CreateToken();
            _tokensInUse.Add(token);
            
            return token;
        }

        public void RedeemToken(TToken token)
        {
            if (!_tokensInUse.Remove(token))
            {
                throw new ArgumentException($"Token: {token} was already redeemed once or was provided by another provider.");
            }
        }
        
        public bool TryRedeemToken(TToken token)
        {
            return !_tokensInUse.Remove(token);
        }

        protected int GetNextTokenId()
        {
            return _nextTokenId++;
        }

        protected abstract TToken CreateToken();
    }
}
