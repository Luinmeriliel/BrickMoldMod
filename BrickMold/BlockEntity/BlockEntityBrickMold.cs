using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    //Is defined in the brickmold.json
    public class MoldContentConfig
    {
        public string Code;
        public JsonItemStack Content;
        public int QuantityPerFillLevel;
        public int MaxFillLevels;
        public string[] ShapesPerFillLevel;
        public string TextureCode;
    }

    
    public class BlockEntityBrickMold : BlockEntityContainer, ITexPositionSource
    {
        internal InventoryGeneric inventory;
        public override InventoryBase Inventory => inventory;
        public override string InventoryClassName => "brickmold";
        ITexPositionSource blockTexPosSource;
        public Size2i AtlasSize => (Api as ICoreClientAPI).BlockTextureAtlas.Size;
        public Vec3d Position => Pos.ToVec3d().Add(0.5, 0.5, 0.5);
        public string Type => "clay";

        MeshData currentMesh;

        string contentCode = "";

        public bool IsFull
        {
            get
            {
                ItemStack[] stacks = GetNonEmptyContentStacks();
                MoldContentConfig config = contentConfigs.FirstOrDefault(c => c.Code == contentCode);
                if (config == null) return false;

                return stacks.Length != 0 && stacks[0].StackSize >= config.QuantityPerFillLevel * config.MaxFillLevels;
            }
        }

        public MoldContentConfig[] contentConfigs => Api.ObjectCache["brickMoldContentConfigs-" + Block.Code] as MoldContentConfig[];

        //Draw filling texture
        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (textureCode != "contents") return blockTexPosSource[textureCode];
                var config = contentConfigs.FirstOrDefault(c => c.Code == contentCode);
                var configTextureCode = config?.TextureCode;
                if (configTextureCode?.Equals("*") == true)
                {
                    configTextureCode = "contents-" + Inventory.FirstNonEmptySlot.Itemstack.Collectible.Code.ToShortString();
                }
                return configTextureCode != null ? blockTexPosSource[configTextureCode] : blockTexPosSource[textureCode];
            }
        }

        public BlockEntityBrickMold()
        {
            inventory = new InventoryGeneric(4, null, null, (id, inv) => new ItemSlotBrickMold(this, inv));
            inventory.OnGetAutoPushIntoSlot = (face, slot) =>
            {
                if (IsFull) return null;
                WeightedSlot wslot = inventory.GetBestSuitedSlot(slot);
                return wslot.slot;
            };
        }

        private ItemStack ResolveWildcardContent(MoldContentConfig config, IWorldAccessor worldAccessor)
        {
            if (config?.Content?.Code == null) return null;
            var searchObjects = new List<CollectibleObject>();

            switch (config.Content.Type)
            {
                case EnumItemClass.Block:
                    searchObjects.AddRange(worldAccessor.SearchBlocks(config.Content.Code));
                    break;
                case EnumItemClass.Item:
                    searchObjects.AddRange(worldAccessor.SearchItems(config.Content.Code));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(config.Content.Type));
            }

            foreach (var item in searchObjects)
            {
                if (item.Code.Equals(Inventory.FirstNonEmptySlot?.Itemstack?.Item?.Code))
                {
                    return new ItemStack(item);
                }
            }

            return null;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (Api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                if (currentMesh == null)
                {
                    currentMesh = GenMesh();
                }
            }

            inventory.SlotModified += Inventory_SlotModified;
        }

        private void Inventory_SlotModified(int id)
        {
            MoldContentConfig config = ItemSlotBrickMold.getContentConfig(Api.World, contentConfigs, inventory[id]);
            this.contentCode = config?.Code;

            if (Api.Side == EnumAppSide.Client) currentMesh = GenMesh();
            MarkDirty(true);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (Api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }


        internal MeshData GenMesh()
        {
            if (Block == null) return null;
            ItemStack firstStack = inventory[0].Itemstack;
            if (firstStack == null) return null;

            string shapeLoc = "";
            ICoreClientAPI capi = Api as ICoreClientAPI;

            if (contentCode == "" || contentConfigs == null)
            {
                return null;
            }
            else
            {
                MoldContentConfig config = contentConfigs.FirstOrDefault(c => c.Code == contentCode);
                if (config == null) return null;

                int fillLevel = Math.Max(0, firstStack.StackSize / config.QuantityPerFillLevel - 1);
                shapeLoc = config.ShapesPerFillLevel[Math.Min(config.ShapesPerFillLevel.Length - 1, fillLevel)];
            }



            Vec3f rotation = new Vec3f(Block.Shape.rotateX, Block.Shape.rotateY, Block.Shape.rotateZ);
            MeshData meshbase;

            blockTexPosSource = capi.Tesselator.GetTextureSource(Block);
            Shape shape = Shape.TryGet(Api, "brickmold:shapes/" + shapeLoc + ".json");
            capi.Tesselator.TesselateShape("bebrickmoldcontentsleft", shape, out meshbase, this, rotation);



            return meshbase;
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            mesher.AddMeshData(currentMesh);
            return false;
        }

        internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot handSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            ItemStack[] stacks = GetNonEmptyContentStacks();
            MoldContentConfig contentConf = ItemSlotBrickMold.getContentConfig(Api.World, contentConfigs, handSlot);

            void PlaySound()
            {
                byPlayer.Entity.World.PlaySoundAt(
                    new AssetLocation("sounds/effect/clayform"),
                    blockSel.Position.X + blockSel.HitPosition.X,
                    blockSel.Position.InternalY + blockSel.HitPosition.Y,
                    blockSel.Position.Z + blockSel.HitPosition.Z,
                    byPlayer,
                    true,
                    8,
                    0.8f);
            }

            if (handSlot.Empty || contentConf == null)
            {
                if (stacks.Length > 0)
                {
                    EmptyMold();
                    PlaySound();
                    return true;
                }
                // Pickup the block here
                return false;
            }    

            // Add new
            if (stacks.Length == 0)
            {
                if (handSlot.StackSize >= contentConf.QuantityPerFillLevel)
                {
                    inventory[0].Itemstack = handSlot.TakeOut(contentConf.QuantityPerFillLevel);
                    inventory[0].MarkDirty();
                    PlaySound();
                    return true;
                }
            return true;
            }

            // Or merge
            bool canAdd =
                // Stack held = stack already in mold
                handSlot.Itemstack.Equals(Api.World, stacks[0], GlobalConstants.IgnoredStackAttributes) &&
                // Stack is >= to amount needed per fill level
                handSlot.StackSize >= contentConf.QuantityPerFillLevel &&
                // Mold contains less than maximum fill level
                stacks[0].StackSize < contentConf.QuantityPerFillLevel * contentConf.MaxFillLevels;

            if (canAdd)
            {
                handSlot.TakeOut(contentConf.QuantityPerFillLevel);
                inventory[0].Itemstack.StackSize += contentConf.QuantityPerFillLevel;
                if (IsFull)
                {
                    string stackCode = inventory[0].Itemstack.Item.Code.ToShortString();        // eg "clay-blue"
                    string[] isVanillaDomain = { "blue", "fire", "tier1", "tier2", "tier3" };   // These use game:
                    string domain = isVanillaDomain.Any(substring => stackCode.Contains(substring)) ? "game:" : "bricklayers:"; // if not game: then bricklayers: (brown and red)
                    string brickType;
                    if (domain == "game:")
                    {
                        brickType = stackCode.Contains("tier") ? "refractorybrick-raw-" : "rawbrick-";  // Check if its refractorybrick
                    }
                    else 
                    {
                        brickType = "rawclaybrick-";    // bricklayers brick
                    }
                    string itemType = stackCode.Split('-')[1];  // eg blue, fire, tier1, red

                    inventory[0].Itemstack.SetFrom(new ItemStack(Api.World.GetItem(new AssetLocation($"{domain}{brickType}{itemType}"))));
                }
                inventory[0].MarkDirty();
                PlaySound();
                return true;
            }
            EmptyMold();
            PlaySound();
            return true;
        }

        internal void EmptyMold()
        {
            Inventory.DropAll(Position);
            inventory[0].MarkDirty();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetString("contentCode", contentCode);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            contentCode = tree.GetString("contentCode");

            if (Api?.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            ItemStack firstStack = inventory[0].Itemstack;

            if (contentConfigs == null)
            {
                return;
            }

            MoldContentConfig config = contentConfigs.FirstOrDefault(c => c.Code == contentCode);

            if (config == null && firstStack != null)
            {
                dsc.AppendLine(firstStack.StackSize + "x " + firstStack.GetName());
            }


            if (config == null || firstStack == null) return;

            int fillLevel = firstStack.StackSize / config.QuantityPerFillLevel;

            dsc.AppendLine(Lang.Get("Portions: {0}", fillLevel));

            ItemStack contentsStack = config.Content.ResolvedItemstack ?? ResolveWildcardContent(config, forPlayer.Entity.World);

            if (contentsStack != null)
            {
                var cobj = contentsStack.Collectible;
                dsc.AppendLine(Lang.Get(contentsStack.GetName()));
            }
        }
    }
}
