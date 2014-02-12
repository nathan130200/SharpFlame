using SharpFlame.Mapping.Tiles;
using SharpFlame.Util;

namespace SharpFlame.Mapping.Tools
{
    public class clsApplyTexture : clsAction
    {
        public int TextureNum;
        public bool SetTexture;
        public TileOrientation Orientation;
        public bool SetOrientation;
        public bool RandomOrientation;
        public enumTextureTerrainAction TerrainAction;

        private clsTerrain Terrain;

        public override void ActionPerform()
        {
            Terrain = Map.Terrain;

            Terrain.Tiles[PosNum.X, PosNum.Y].Terrain_IsCliff = false;
            Terrain.Tiles[PosNum.X, PosNum.Y].TriTopLeftIsCliff = false;
            Terrain.Tiles[PosNum.X, PosNum.Y].TriTopRightIsCliff = false;
            Terrain.Tiles[PosNum.X, PosNum.Y].TriBottomLeftIsCliff = false;
            Terrain.Tiles[PosNum.X, PosNum.Y].TriBottomRightIsCliff = false;

            if ( SetTexture )
            {
                Terrain.Tiles[PosNum.X, PosNum.Y].Texture.TextureNum = TextureNum;
            }
            if ( SetOrientation )
            {
                if ( RandomOrientation )
                {
                    Terrain.Tiles[PosNum.X, PosNum.Y].Texture.Orientation = new TileOrientation(App.Random.Next() < 0.5F, App.Random.Next() < 0.5F,
                        App.Random.Next() < 0.5F);
                }
                else
                {
                    Terrain.Tiles[PosNum.X, PosNum.Y].Texture.Orientation = Orientation;
                }
            }

            Map.TileTextureChangeTerrainAction(PosNum, TerrainAction);

            Map.SectorGraphicsChanges.TileChanged(PosNum);
            Map.SectorTerrainUndoChanges.TileChanged(PosNum);
        }
    }
}