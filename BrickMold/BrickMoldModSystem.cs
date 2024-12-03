using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace BrickMold
{
    public class BrickMoldModSystem : ModSystem
    {

        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockClass("brickmold.brickmold", typeof(BlockBrickMold));
            api.RegisterBlockEntityClass("brickmold.BlockEntityBrickMold", typeof(BlockEntityBrickMold));
            
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
        }

    }
}
