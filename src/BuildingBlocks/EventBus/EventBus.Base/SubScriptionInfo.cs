using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventBus.Base
{
    public class SubScriptionInfo
    {
        public Type HandleType { get;}

        public SubScriptionInfo(Type handleType)
        {
            HandleType = handleType ?? throw new ArgumentNullException(nameof(handleType));
        }

        public static SubScriptionInfo Typed(Type handleType)
        {
            return new SubScriptionInfo(handleType);
        }
    }


}
