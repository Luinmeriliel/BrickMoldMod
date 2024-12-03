using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemSlotBrickMold : ItemSlotSurvival
    {
        BlockEntityBrickMold be;

        public ItemSlotBrickMold(BlockEntityBrickMold be, InventoryGeneric inventory) : base(inventory)
        {
            this.be = be;
        }

        public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
        {
            return base.CanTakeFrom(sourceSlot, priority) && moldable(sourceSlot);
        }

        public override bool CanHold(ItemSlot itemstackFromSourceSlot)
        {
            return base.CanHold(itemstackFromSourceSlot) && moldable(itemstackFromSourceSlot);
        }

        public bool moldable(ItemSlot sourceSlot)
        {
            if (!Empty && !sourceSlot.Itemstack.Equals(be.Api.World, itemstack, GlobalConstants.IgnoredStackAttributes)) return false;

            MoldContentConfig[] contentConfigs = be.contentConfigs;
            MoldContentConfig config = getContentConfig(be.Api.World, contentConfigs, sourceSlot);

            return config != null && config.MaxFillLevels * config.QuantityPerFillLevel > StackSize;
        }


        public static MoldContentConfig getContentConfig(IWorldAccessor world, MoldContentConfig[] contentConfigs, ItemSlot sourceSlot)
        {
            if (sourceSlot.Empty) return null;

            for (int i = 0; i < contentConfigs.Length; i++)
            {
                var cfg = contentConfigs[i];

                if (cfg.Content.Code.Path.Contains('*'))
                {
                    if (WildcardUtil.Match(cfg.Content.Code, sourceSlot.Itemstack.Collectible.Code)) return cfg;
                    continue;
                }

                if (sourceSlot.Itemstack.Equals(world, cfg.Content.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes))
                {
                    return cfg;
                }
            }

            return null;
        }
    }
}
