using System.Threading.Tasks;

namespace StateMachine
{
    public delegate ValueTask StateChangeRequestAsync(StateTransferInfo info);
}
