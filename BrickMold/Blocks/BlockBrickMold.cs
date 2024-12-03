using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockBrickMoldBase : Block
    {
        public MoldContentConfig[] contentConfigs;
        public WorldInteraction[] placeInteractionHelp;

        public BlockPos RootOffset = new BlockPos();

        protected string[] unsuitableEntityCodesBeginsWith = new string[0];
        protected string[] unsuitableEntityCodesExact;
        protected string unsuitableEntityFirstLetters = "";

        public void init()
        {
            // Get allowed contents from brickmold.json "contentConfig"
            contentConfigs = ObjectCacheUtil.GetOrCreate(api, "brickMoldContentConfigs-" + Code, () =>
            {
                var cfgs = Attributes?["contentConfig"]?.AsObject<MoldContentConfig[]>();
                if (cfgs == null) return null;

                foreach (var val in cfgs)
                {
                    if (!val.Content.Code.Path.Contains('*'))
                    {
                        val.Content.Resolve(api.World, "brickmoldcontentconfig");
                    }
                }

                return cfgs;
            });


            List<ItemStack> allowedstacks = new List<ItemStack>();
            foreach (var val in contentConfigs)
            {
                if (val.Content.Code.Path.Contains('*'))
                {
                    if (val.Content.Type == EnumItemClass.Block)
                    {
                        allowedstacks.AddRange(api.World.SearchBlocks(val.Content.Code).Select(block => new ItemStack(block, val.QuantityPerFillLevel)));
                    }
                    else
                    {
                        allowedstacks.AddRange(api.World.SearchItems(val.Content.Code).Select(item => new ItemStack(item, val.QuantityPerFillLevel)));
                    }
                }
                else
                {
                    if (val.Content.ResolvedItemstack == null) continue;

                    var stack = val.Content.ResolvedItemstack.Clone();
                    stack.StackSize = val.QuantityPerFillLevel;
                    allowedstacks.Add(stack);
                }
            }

            placeInteractionHelp = new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "Add clay",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = allowedstacks.ToArray(),
                    GetMatchingStacks = (wi, bs, es) => {
                        BlockEntityBrickMold bebm = api.World.BlockAccessor.GetBlockEntity(bs.Position + RootOffset) as BlockEntityBrickMold;
                        if (bebm?.IsFull != false) return null;

                        ItemStack[] stacks = bebm.GetNonEmptyContentStacks();
                        if (stacks != null && stacks.Length != 0) return stacks;

                        return wi.Itemstacks;
                    }

                }
                //// Overwrite vanilla "blockhelp-collect" to add Ctrl key information
                //new WorldInteraction()
                //{
                //    ActionLangCode = "blockhelp-collect",
                //    MouseButton = EnumMouseButton.Left,
                //    HotKeyCode = "ctrl", // Inform about the Ctrl key
                //}

            };

        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return placeInteractionHelp.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }


    }


    public class BlockBrickMold : BlockBrickMoldBase
    {

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            init();
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel != null)
            {
                BlockPos pos = blockSel.Position;

                BlockEntityBrickMold bebm = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityBrickMold;
                if (bebm != null)
                {
                    bool ok = bebm.OnInteract(byPlayer, blockSel);
                    if (ok)
                    {
                        if (world.Side == EnumAppSide.Client) (byPlayer as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                    }
                    else
                    {
                        return base.OnBlockInteractStart(world, byPlayer, blockSel);
                    }
                    return ok;
                }
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }


        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            bool flip = Math.Abs(angle) == 90 || Math.Abs(angle) == 270;

            if (flip)
            {
                string orient = Variant["side"];

                return CodeWithVariant("side", orient == "we" ? "ns" : "we");
            }

            return Code;
        }


        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis)
        {
            BlockFacing facing = BlockFacing.FromCode(LastCodePart());
            if (facing.Axis == axis)
            {
                return CodeWithParts(facing.Opposite.Code);
            }
            return Code;
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            BlockEntityBrickMold bebm = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityBrickMold;
            if (bebm != null)
            {
                StringBuilder dsc = new StringBuilder();
                bebm.GetBlockInfo(forPlayer, dsc);
                return dsc.ToString();
            }
            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }
    }
}
