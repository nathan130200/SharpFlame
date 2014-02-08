using System;
using System.Windows.Forms;
using Matrix3D;
using Microsoft.VisualBasic;
using SharpFlame.Collections;
using SharpFlame.Controls;
using SharpFlame.FileIO;
using SharpFlame.Mapping;
using SharpFlame.Mapping.Objects;
using SharpFlame.Mapping.Script;
using SharpFlame.Mapping.Tools;
using SharpFlame.Maths;
using SharpFlame.Painters;
using SharpFlame.AppSettings;
using SharpFlame.Util;

namespace SharpFlame
{
    public class clsViewInfo
    {
        public clsMap Map;
        public MapViewControl MapViewControl;

        public sXYZ_int ViewPos;
        public Matrix3DMath.Matrix3D ViewAngleMatrix = new Matrix3DMath.Matrix3D();
        public Matrix3DMath.Matrix3D ViewAngleMatrix_Inverted = new Matrix3DMath.Matrix3D();
        public Angles.AngleRPY ViewAngleRPY;
        public double FOVMultiplier;
        public double FOVMultiplierExponent;
        public float FieldOfViewY;

        public clsViewInfo(clsMap Map, MapViewControl MapViewControl)
        {
            this.Map = Map;
            this.MapViewControl = MapViewControl;

            ViewPos = new sXYZ_int(0, 3072, 0);
            FOV_Multiplier_Set(SettingsManager.Settings.FOVDefault);
            ViewAngleSetToDefault();
            LookAtPos(new sXY_int((int)(Map.Terrain.TileSize.X * App.TerrainGridSpacing / 2.0D),
                (int)(Map.Terrain.TileSize.Y * App.TerrainGridSpacing / 2.0D)));
        }

        public void FOV_Scale_2E_Set(double Power)
        {
            FOVMultiplierExponent = Power;
            FOVMultiplier = Math.Pow(2.0D, FOVMultiplierExponent);

            FOV_Calc();
        }

        public void FOV_Scale_2E_Change(double PowerChange)
        {
            FOVMultiplierExponent += PowerChange;
            FOVMultiplier = Math.Pow(2.0D, FOVMultiplierExponent);

            FOV_Calc();
        }

        public void FOV_Set(double Radians, MapViewControl MapViewControl)
        {
            FOVMultiplier = Math.Tan(Radians / 2.0D) / MapViewControl.GLSize.Y * 2.0D;
            FOVMultiplierExponent = Math.Log(FOVMultiplier) / Math.Log(2.0D);

            FOV_Calc();
        }

        public void FOV_Multiplier_Set(double Value)
        {
            FOVMultiplier = Value;
            FOVMultiplierExponent = Math.Log(FOVMultiplier) / Math.Log(2.0D);

            FOV_Calc();
        }

        public void FOV_Calc()
        {
            const float Min = (float)(0.1d * MathUtil.RadOf1Deg);
            const float Max = (float)(179.0d * MathUtil.RadOf1Deg);

            FieldOfViewY = (float)(Math.Atan(MapViewControl.GLSize.Y * FOVMultiplier / 2.0D) * 2.0D);
            if ( FieldOfViewY < Min )
            {
                FieldOfViewY = Min;
                if ( MapViewControl.GLSize.Y > 0 )
                {
                    FOVMultiplier = 2.0D * Math.Tan(FieldOfViewY / 2.0D) / MapViewControl.GLSize.Y;
                    FOVMultiplierExponent = Math.Log(FOVMultiplier) / Math.Log(2.0D);
                }
            }
            else if ( FieldOfViewY > Max )
            {
                FieldOfViewY = Max;
                if ( MapViewControl.GLSize.Y > 0 )
                {
                    FOVMultiplier = 2.0D * Math.Tan(FieldOfViewY / 2.0D) / MapViewControl.GLSize.Y;
                    FOVMultiplierExponent = Math.Log(FOVMultiplier) / Math.Log(2.0D);
                }
            }

            MapViewControl.DrawViewLater();
        }

        public void ViewPosSet(sXYZ_int NewViewPos)
        {
            ViewPos = NewViewPos;
            ViewPosClamp();

            MapViewControl.DrawViewLater();
        }

        public void ViewPosChange(sXYZ_int Displacement)
        {
            ViewPos.X += Displacement.X;
            ViewPos.Z += Displacement.Z;
            ViewPos.Y += Displacement.Y;
            ViewPosClamp();

            MapViewControl.DrawViewLater();
        }

        private void ViewPosClamp()
        {
            const int MaxHeight = 1048576;
            const int MaxDist = 1048576;

            ViewPos.X = MathUtil.Clamp_int(ViewPos.X, Convert.ToInt32(- MaxDist), Map.Terrain.TileSize.X * App.TerrainGridSpacing + MaxDist);
            ViewPos.Z = MathUtil.Clamp_int(ViewPos.Z, - Map.Terrain.TileSize.Y * App.TerrainGridSpacing - MaxDist, MaxDist);
            ViewPos.Y = MathUtil.Clamp_int(ViewPos.Y, ((int)(Math.Ceiling(Map.GetTerrainHeight(new sXY_int(ViewPos.X, - ViewPos.Z))))) + 16, MaxHeight);
        }

        public void ViewAngleSet(Matrix3DMath.Matrix3D NewMatrix)
        {
            Matrix3DMath.MatrixCopy(NewMatrix, ViewAngleMatrix);
            Matrix3DMath.MatrixNormalize(ViewAngleMatrix);
            Matrix3DMath.MatrixInvert(ViewAngleMatrix, ViewAngleMatrix_Inverted);
            Matrix3DMath.MatrixToRPY(ViewAngleMatrix, ref ViewAngleRPY);

            MapViewControl.DrawViewLater();
        }

        public void ViewAngleSetToDefault()
        {
            Matrix3DMath.Matrix3D matrixA = new Matrix3DMath.Matrix3D();
            Matrix3DMath.MatrixSetToXAngle(matrixA, Math.Atan(2.0D));
            ViewAngleSet(matrixA);

            MapViewControl.DrawViewLater();
        }

        public void ViewAngleSet_Rotate(Matrix3DMath.Matrix3D NewMatrix)
        {
            bool Flag = default(bool);
            Position.XYZ_dbl XYZ_dbl = default(Position.XYZ_dbl);
            Position.XYZ_dbl XYZ_dbl2 = default(Position.XYZ_dbl);
            //Dim XYZ_lng As sXYZ_lng
            Position.XY_dbl XY_dbl = default(Position.XY_dbl);

            if ( App.ViewMoveType == enumView_Move_Type.RTS & App.RTSOrbit )
            {
                Flag = true;
                //If ScreenXY_Get_TerrainPos(CInt(Int(GLSize.X / 2.0#)), CInt(Int(GLSize.Y / 2.0#)), XYZ_lng) Then
                //    XYZ_dbl.X = XYZ_lng.X
                //    XYZ_dbl.Y = XYZ_lng.Y
                //    XYZ_dbl.Z = XYZ_lng.Z
                //Else
                if ( ScreenXY_Get_ViewPlanePos_ForwardDownOnly((int)(Conversion.Int(MapViewControl.GLSize.X / 2.0D)), (int)(Conversion.Int(MapViewControl.GLSize.Y / 2.0D)), 127.5D,
                    ref XY_dbl) )
                {
                    XYZ_dbl.X = XY_dbl.X;
                    XYZ_dbl.Y = 127.5D;
                    XYZ_dbl.Z = Convert.ToDouble(- XY_dbl.Y);
                }
                else
                {
                    Flag = false;
                }
                //End If
            }
            else
            {
                Flag = false;
            }

            Matrix3DMath.MatrixToRPY(NewMatrix, ref ViewAngleRPY);
            if ( Flag )
            {
                if ( ViewAngleRPY.Pitch < MathUtil.RadOf1Deg * 10.0D )
                {
                    ViewAngleRPY.Pitch = MathUtil.RadOf1Deg * 10.0D;
                }
            }
            Matrix3DMath.MatrixSetToRPY(ViewAngleMatrix, ViewAngleRPY);
            Matrix3DMath.MatrixInvert(ViewAngleMatrix, ViewAngleMatrix_Inverted);

            if ( Flag )
            {
                XYZ_dbl2.X = ViewPos.X;
                XYZ_dbl2.Y = ViewPos.Y;
                XYZ_dbl2.Z = Convert.ToDouble(- ViewPos.Z);
                MoveToViewTerrainPosFromDistance(XYZ_dbl, Convert.ToDouble((XYZ_dbl2 - XYZ_dbl).GetMagnitude()));
            }

            MapViewControl.DrawViewLater();
        }

        public void LookAtTile(sXY_int TileNum)
        {
            sXY_int Pos = new sXY_int();

            Pos.X = (int)((TileNum.X + 0.5D) * App.TerrainGridSpacing);
            Pos.Y = (int)((TileNum.Y + 0.5D) * App.TerrainGridSpacing);
            LookAtPos(Pos);
        }

        public void LookAtPos(sXY_int Horizontal)
        {
            Position.XYZ_dbl XYZ_dbl = default(Position.XYZ_dbl);
            sXYZ_int XYZ_int = new sXYZ_int();
            double dblTemp = 0;
            int A = 0;
            Matrix3DMath.Matrix3D matrixA = new Matrix3DMath.Matrix3D();
            Angles.AnglePY AnglePY = default(Angles.AnglePY);

            Matrix3DMath.VectorForwardsRotationByMatrix(ViewAngleMatrix, ref XYZ_dbl);
            dblTemp = Map.GetTerrainHeight(Horizontal);
            A = ((int)(Math.Ceiling(dblTemp))) + 128;
            if ( ViewPos.Y < A )
            {
                ViewPos.Y = A;
            }
            if ( XYZ_dbl.Y > -0.33333333333333331D )
            {
                XYZ_dbl.Y = -0.33333333333333331D;
                Matrix3DMath.VectorToPY(XYZ_dbl, ref AnglePY);
                Matrix3DMath.MatrixSetToPY(matrixA, AnglePY);
                ViewAngleSet(matrixA);
            }
            dblTemp = (ViewPos.Y - dblTemp) / XYZ_dbl.Y;

            XYZ_int.X = (int)(Horizontal.X + dblTemp * XYZ_dbl.X);
            XYZ_int.Y = ViewPos.Y;
            XYZ_int.Z = (int)(- Horizontal.Y + dblTemp * XYZ_dbl.Z);

            ViewPosSet(XYZ_int);
        }

        public void MoveToViewTerrainPosFromDistance(Position.XYZ_dbl TerrainPos, double Distance)
        {
            Position.XYZ_dbl XYZ_dbl = default(Position.XYZ_dbl);
            sXYZ_int XYZ_int = new sXYZ_int();

            Matrix3DMath.VectorForwardsRotationByMatrix(ViewAngleMatrix, ref XYZ_dbl);

            XYZ_int.X = (int)(TerrainPos.X - XYZ_dbl.X * Distance);
            XYZ_int.Y = (int)(TerrainPos.Y - XYZ_dbl.Y * Distance);
            XYZ_int.Z = (int)(- TerrainPos.Z - XYZ_dbl.Z * Distance);

            ViewPosSet(XYZ_int);
        }

        public bool Pos_Get_Screen_XY(Position.XYZ_dbl Pos, ref sXY_int Result)
        {
            if ( Pos.Z <= 0.0D )
            {
                return false;
            }

            try
            {
                double RatioZ_px = 1.0D / (FOVMultiplier * Pos.Z);
                Result.X = (int)(MapViewControl.GLSize.X / 2.0D + (Pos.X * RatioZ_px));
                Result.Y = (int)(MapViewControl.GLSize.Y / 2.0D - (Pos.Y * RatioZ_px));
                return true;
            }
            catch
            {
            }

            return false;
        }

        public bool ScreenXY_Get_ViewPlanePos(sXY_int ScreenPos, double PlaneHeight, ref Position.XY_dbl ResultPos)
        {
            double dblTemp = 0;
            Position.XYZ_dbl XYZ_dbl = default(Position.XYZ_dbl);
            Position.XYZ_dbl XYZ_dbl2 = default(Position.XYZ_dbl);

            try
            {
                //convert screen pos to vector of one pos unit
                XYZ_dbl.X = (ScreenPos.X - MapViewControl.GLSize.X / 2.0D) * FOVMultiplier;
                XYZ_dbl.Y = (MapViewControl.GLSize.Y / 2.0D - ScreenPos.Y) * FOVMultiplier;
                XYZ_dbl.Z = 1.0D;
                //factor in the view angle
                Matrix3DMath.VectorRotationByMatrix(ViewAngleMatrix, XYZ_dbl, ref XYZ_dbl2);
                //get distance to cover the height
                dblTemp = (PlaneHeight - ViewPos.Y) / XYZ_dbl2.Y;
                ResultPos.X = ViewPos.X + XYZ_dbl2.X * dblTemp;
                ResultPos.Y = ViewPos.Z + XYZ_dbl2.Z * dblTemp;
            }
            catch
            {
                return false;
            }
            return true;
        }

        public bool ScreenXY_Get_TerrainPos(sXY_int ScreenPos, ref sWorldPos ResultPos)
        {
            double dblTemp = 0;
            Position.XYZ_dbl XYZ_dbl = default(Position.XYZ_dbl);
            Position.XYZ_dbl TerrainViewVector = default(Position.XYZ_dbl);
            int X = 0;
            int Y = 0;
            Position.XY_dbl LimitA = default(Position.XY_dbl);
            Position.XY_dbl LimitB = default(Position.XY_dbl);
            sXY_int Min = new sXY_int();
            sXY_int Max = new sXY_int();
            double TriGradientX = 0;
            double TriGradientZ = 0;
            double TriHeightOffset = 0;
            double Dist = 0;
            Position.XYZ_dbl BestPos = default(Position.XYZ_dbl);
            double BestDist = 0;
            Position.XYZ_dbl Dif = default(Position.XYZ_dbl);
            double InTileX = 0;
            double InTileZ = 0;
            Position.XY_dbl TilePos = default(Position.XY_dbl);
            Position.XYZ_dbl TerrainViewPos = default(Position.XYZ_dbl);

            try
            {
                TerrainViewPos.X = ViewPos.X;
                TerrainViewPos.Y = ViewPos.Y;
                TerrainViewPos.Z = Convert.ToDouble(- ViewPos.Z);

                //convert screen pos to vector of one pos unit
                XYZ_dbl.X = (ScreenPos.X - MapViewControl.GLSize.X / 2.0D) * FOVMultiplier;
                XYZ_dbl.Y = (MapViewControl.GLSize.Y / 2.0D - ScreenPos.Y) * FOVMultiplier;
                XYZ_dbl.Z = 1.0D;
                //rotate the vector so that it points forward and level
                Matrix3DMath.VectorRotationByMatrix(ViewAngleMatrix, XYZ_dbl, ref TerrainViewVector);
                TerrainViewVector.Y = Convert.ToDouble(- TerrainViewVector.Y); //get the amount of looking down, not up
                TerrainViewVector.Z = Convert.ToDouble(- TerrainViewVector.Z); //convert to terrain coordinates from view coordinates
                //get range of possible tiles
                dblTemp = (TerrainViewPos.Y - 255 * Map.HeightMultiplier) / TerrainViewVector.Y;
                LimitA.X = TerrainViewPos.X + TerrainViewVector.X * dblTemp;
                LimitA.Y = TerrainViewPos.Z + TerrainViewVector.Z * dblTemp;
                dblTemp = TerrainViewPos.Y / TerrainViewVector.Y;
                LimitB.X = TerrainViewPos.X + TerrainViewVector.X * dblTemp;
                LimitB.Y = TerrainViewPos.Z + TerrainViewVector.Z * dblTemp;
                Min.X = Math.Max(Convert.ToInt32(Conversion.Int(Math.Min(LimitA.X, LimitB.X) / App.TerrainGridSpacing)), 0);
                Min.Y = Math.Max((int)(Conversion.Int(Math.Min(LimitA.Y, LimitB.Y) / App.TerrainGridSpacing)), 0);
                Max.X = Math.Min(Convert.ToInt32(Conversion.Int(Math.Max(LimitA.X, LimitB.X) / App.TerrainGridSpacing)), Map.Terrain.TileSize.X - 1);
                Max.Y = Math.Min(Convert.ToInt32(Conversion.Int(Math.Max(LimitA.Y, LimitB.Y) / App.TerrainGridSpacing)), Map.Terrain.TileSize.Y - 1);
                //find the nearest valid tile to the view
                BestDist = double.MaxValue;
                BestPos.X = double.NaN;
                BestPos.Y = double.NaN;
                BestPos.Z = double.NaN;
                for ( Y = Min.Y; Y <= Max.Y; Y++ )
                {
                    for ( X = Min.X; X <= Max.X; X++ )
                    {
                        TilePos.X = X * App.TerrainGridSpacing;
                        TilePos.Y = Y * App.TerrainGridSpacing;

                        if ( Map.Terrain.Tiles[X, Y].Tri )
                        {
                            TriHeightOffset = Convert.ToDouble(Map.Terrain.Vertices[X, Y].Height * Map.HeightMultiplier);
                            TriGradientX = Convert.ToDouble(Map.Terrain.Vertices[X + 1, Y].Height * Map.HeightMultiplier - TriHeightOffset);
                            TriGradientZ = Convert.ToDouble(Map.Terrain.Vertices[X, Y + 1].Height * Map.HeightMultiplier - TriHeightOffset);
                            XYZ_dbl.Y = (TriHeightOffset +
                                         (TriGradientX * (TerrainViewPos.X - TilePos.X) + TriGradientZ * (TerrainViewPos.Z - TilePos.Y) +
                                          (TriGradientX * TerrainViewVector.X + TriGradientZ * TerrainViewVector.Z) * TerrainViewPos.Y / TerrainViewVector.Y) /
                                         App.TerrainGridSpacing) /
                                        (1.0D +
                                         (TriGradientX * TerrainViewVector.X + TriGradientZ * TerrainViewVector.Z) / (TerrainViewVector.Y * App.TerrainGridSpacing));
                            XYZ_dbl.X = TerrainViewPos.X + TerrainViewVector.X * (TerrainViewPos.Y - XYZ_dbl.Y) / TerrainViewVector.Y;
                            XYZ_dbl.Z = TerrainViewPos.Z + TerrainViewVector.Z * (TerrainViewPos.Y - XYZ_dbl.Y) / TerrainViewVector.Y;
                            InTileX = XYZ_dbl.X / App.TerrainGridSpacing - X;
                            InTileZ = XYZ_dbl.Z / App.TerrainGridSpacing - Y;
                            if ( InTileZ <= 1.0D - InTileX & InTileX >= 0.0D & InTileZ >= 0.0D & InTileX <= 1.0D & InTileZ <= 1.0D )
                            {
                                Dif = XYZ_dbl - TerrainViewPos;
                                Dist = Dif.GetMagnitude();
                                if ( Dist < BestDist )
                                {
                                    BestDist = Dist;
                                    BestPos = XYZ_dbl;
                                }
                            }

                            TriHeightOffset = Convert.ToDouble(Map.Terrain.Vertices[X + 1, Y + 1].Height * Map.HeightMultiplier);
                            TriGradientX = Convert.ToDouble(Map.Terrain.Vertices[X, Y + 1].Height * Map.HeightMultiplier - TriHeightOffset);
                            TriGradientZ = Convert.ToDouble(Map.Terrain.Vertices[X + 1, Y].Height * Map.HeightMultiplier - TriHeightOffset);
                            XYZ_dbl.Y = (TriHeightOffset + TriGradientX + TriGradientZ +
                                         (TriGradientX * (TilePos.X - TerrainViewPos.X) + TriGradientZ * (TilePos.Y - TerrainViewPos.Z) -
                                          (TriGradientX * TerrainViewVector.X + TriGradientZ * TerrainViewVector.Z) * TerrainViewPos.Y / TerrainViewVector.Y) /
                                         App.TerrainGridSpacing) /
                                        (1.0D -
                                         (TriGradientX * TerrainViewVector.X + TriGradientZ * TerrainViewVector.Z) / (TerrainViewVector.Y * App.TerrainGridSpacing));
                            XYZ_dbl.X = TerrainViewPos.X + TerrainViewVector.X * (TerrainViewPos.Y - XYZ_dbl.Y) / TerrainViewVector.Y;
                            XYZ_dbl.Z = TerrainViewPos.Z + TerrainViewVector.Z * (TerrainViewPos.Y - XYZ_dbl.Y) / TerrainViewVector.Y;
                            InTileX = XYZ_dbl.X / App.TerrainGridSpacing - X;
                            InTileZ = XYZ_dbl.Z / App.TerrainGridSpacing - Y;
                            if ( InTileZ >= 1.0D - InTileX & InTileX >= 0.0D & InTileZ >= 0.0D & InTileX <= 1.0D & InTileZ <= 1.0D )
                            {
                                Dif = XYZ_dbl - TerrainViewPos;
                                Dist = Dif.GetMagnitude();
                                if ( Dist < BestDist )
                                {
                                    BestDist = Dist;
                                    BestPos = XYZ_dbl;
                                }
                            }
                        }
                        else
                        {
                            TriHeightOffset = Convert.ToDouble(Map.Terrain.Vertices[X + 1, Y].Height * Map.HeightMultiplier);
                            TriGradientX = Convert.ToDouble(Map.Terrain.Vertices[X, Y].Height * Map.HeightMultiplier - TriHeightOffset);
                            TriGradientZ = Convert.ToDouble(Map.Terrain.Vertices[X + 1, Y + 1].Height * Map.HeightMultiplier - TriHeightOffset);
                            XYZ_dbl.Y = (TriHeightOffset + TriGradientX +
                                         (TriGradientX * (TilePos.X - TerrainViewPos.X) + TriGradientZ * (TerrainViewPos.Z - TilePos.Y) -
                                          (TriGradientX * TerrainViewVector.X - TriGradientZ * TerrainViewVector.Z) * TerrainViewPos.Y / TerrainViewVector.Y) /
                                         App.TerrainGridSpacing) /
                                        (1.0D -
                                         (TriGradientX * TerrainViewVector.X - TriGradientZ * TerrainViewVector.Z) / (TerrainViewVector.Y * App.TerrainGridSpacing));
                            XYZ_dbl.X = TerrainViewPos.X + TerrainViewVector.X * (TerrainViewPos.Y - XYZ_dbl.Y) / TerrainViewVector.Y;
                            XYZ_dbl.Z = TerrainViewPos.Z + TerrainViewVector.Z * (TerrainViewPos.Y - XYZ_dbl.Y) / TerrainViewVector.Y;
                            InTileX = XYZ_dbl.X / App.TerrainGridSpacing - X;
                            InTileZ = XYZ_dbl.Z / App.TerrainGridSpacing - Y;
                            if ( InTileZ <= InTileX & InTileX >= 0.0D & InTileZ >= 0.0D & InTileX <= 1.0D & InTileZ <= 1.0D )
                            {
                                Dif = XYZ_dbl - TerrainViewPos;
                                Dist = Dif.GetMagnitude();
                                if ( Dist < BestDist )
                                {
                                    BestDist = Dist;
                                    BestPos = XYZ_dbl;
                                }
                            }

                            TriHeightOffset = Convert.ToDouble(Map.Terrain.Vertices[X, Y + 1].Height * Map.HeightMultiplier);
                            TriGradientX = Convert.ToDouble(Map.Terrain.Vertices[X + 1, Y + 1].Height * Map.HeightMultiplier - TriHeightOffset);
                            TriGradientZ = Convert.ToDouble(Map.Terrain.Vertices[X, Y].Height * Map.HeightMultiplier - TriHeightOffset);
                            XYZ_dbl.Y = (TriHeightOffset + TriGradientZ +
                                         (TriGradientX * (TerrainViewPos.X - TilePos.X) + TriGradientZ * (TilePos.Y - TerrainViewPos.Z) +
                                          (TriGradientX * TerrainViewVector.X - TriGradientZ * TerrainViewVector.Z) * TerrainViewPos.Y / TerrainViewVector.Y) /
                                         App.TerrainGridSpacing) /
                                        (1.0D +
                                         (TriGradientX * TerrainViewVector.X - TriGradientZ * TerrainViewVector.Z) / (TerrainViewVector.Y * App.TerrainGridSpacing));
                            XYZ_dbl.X = TerrainViewPos.X + TerrainViewVector.X * (TerrainViewPos.Y - XYZ_dbl.Y) / TerrainViewVector.Y;
                            XYZ_dbl.Z = TerrainViewPos.Z + TerrainViewVector.Z * (TerrainViewPos.Y - XYZ_dbl.Y) / TerrainViewVector.Y;
                            InTileX = XYZ_dbl.X / App.TerrainGridSpacing - X;
                            InTileZ = XYZ_dbl.Z / App.TerrainGridSpacing - Y;
                            if ( InTileZ >= InTileX & InTileX >= 0.0D & InTileZ >= 0.0D & InTileX <= 1.0D & InTileZ <= 1.0D )
                            {
                                Dif = XYZ_dbl - TerrainViewPos;
                                Dist = Dif.GetMagnitude();
                                if ( Dist < BestDist )
                                {
                                    BestDist = Dist;
                                    BestPos = XYZ_dbl;
                                }
                            }
                        }
                    }
                }

                if ( BestPos.X == double.NaN )
                {
                    return false;
                }

                ResultPos.Horizontal.X = (int)BestPos.X;
                ResultPos.Altitude = (int)BestPos.Y;
                ResultPos.Horizontal.Y = (int)BestPos.Z;
            }
            catch
            {
                return false;
            }
            return true;
        }

        public bool ScreenXY_Get_ViewPlanePos_ForwardDownOnly(int ScreenX, int ScreenY, double PlaneHeight, ref Position.XY_dbl ResultPos)
        {
            double dblTemp = 0;
            Position.XYZ_dbl XYZ_dbl = default(Position.XYZ_dbl);
            Position.XYZ_dbl XYZ_dbl2 = default(Position.XYZ_dbl);
            double dblTemp2 = 0;

            if ( ViewPos.Y < PlaneHeight )
            {
                return false;
            }

            try
            {
                //convert screen pos to vector of one pos unit
                dblTemp2 = FOVMultiplier;
                XYZ_dbl.X = (ScreenX - MapViewControl.GLSize.X / 2.0D) * dblTemp2;
                XYZ_dbl.Y = (MapViewControl.GLSize.Y / 2.0D - ScreenY) * dblTemp2;
                XYZ_dbl.Z = 1.0D;
                //factor in the view angle
                Matrix3DMath.VectorRotationByMatrix(ViewAngleMatrix, XYZ_dbl, ref XYZ_dbl2);
                //get distance to cover the height
                if ( XYZ_dbl2.Y > 0.0D )
                {
                    return false;
                }
                dblTemp = (PlaneHeight - ViewPos.Y) / XYZ_dbl2.Y;
                ResultPos.X = ViewPos.X + XYZ_dbl2.X * dblTemp;
                ResultPos.Y = ViewPos.Z + XYZ_dbl2.Z * dblTemp;
            }
            catch
            {
                return false;
            }
            return true;
        }

        public double Tiles_Per_Minimap_Pixel;

        public void MouseOver_Pos_Calc()
        {
            Position.XY_dbl XY_dbl = default(Position.XY_dbl);
            bool Flag = default(bool);
            sXY_int Footprint = new sXY_int();
            clsMouseDown.clsOverMinimap MouseLeftDownOverMinimap = GetMouseLeftDownOverMinimap();

            if ( MouseLeftDownOverMinimap != null )
            {
                if ( MouseOver == null )
                {
                }
                else if ( IsViewPosOverMinimap(MouseOver.ScreenPos) )
                {
                    sXY_int Pos = new sXY_int((int)Conversion.Int(MouseOver.ScreenPos.X * Tiles_Per_Minimap_Pixel),
                        (int)(Conversion.Int(MouseOver.ScreenPos.Y * Tiles_Per_Minimap_Pixel)));
                    Map.TileNumClampToMap(Pos);
                    LookAtTile(Pos);
                }
            }
            else
            {
                clsMouseOver.clsOverTerrain MouseOverTerrain = new clsMouseOver.clsOverTerrain();
                Flag = false;
                if ( SettingsManager.Settings.DirectPointer )
                {
                    if ( ScreenXY_Get_TerrainPos(MouseOver.ScreenPos, ref MouseOverTerrain.Pos) )
                    {
                        if ( Map.PosIsOnMap(MouseOverTerrain.Pos.Horizontal) )
                        {
                            Flag = true;
                        }
                    }
                }
                else
                {
                    MouseOverTerrain.Pos.Altitude = (int)(255.0D / 2.0D * Map.HeightMultiplier);
                    if ( ScreenXY_Get_ViewPlanePos(MouseOver.ScreenPos, MouseOverTerrain.Pos.Altitude, ref XY_dbl) )
                    {
                        MouseOverTerrain.Pos.Horizontal.X = (int)XY_dbl.X;
                        MouseOverTerrain.Pos.Horizontal.Y = Convert.ToInt32(- XY_dbl.Y);
                        if ( Map.PosIsOnMap(MouseOverTerrain.Pos.Horizontal) )
                        {
                            MouseOverTerrain.Pos.Altitude = (int)(Map.GetTerrainHeight(MouseOverTerrain.Pos.Horizontal));
                            Flag = true;
                        }
                    }
                }
                if ( Flag )
                {
                    MouseOver.OverTerrain = MouseOverTerrain;
                    MouseOverTerrain.Tile.Normal.X = (int)Conversion.Int((double)MouseOverTerrain.Pos.Horizontal.X / App.TerrainGridSpacing);
                    MouseOverTerrain.Tile.Normal.Y = (int)(Conversion.Int((double)MouseOverTerrain.Pos.Horizontal.Y / App.TerrainGridSpacing));
                    MouseOverTerrain.Vertex.Normal.X = (int)( Math.Round( ( (double)MouseOverTerrain.Pos.Horizontal.X / App.TerrainGridSpacing ) ) );
                    MouseOverTerrain.Vertex.Normal.Y = (int)( Math.Round( ( (double)MouseOverTerrain.Pos.Horizontal.Y / App.TerrainGridSpacing ) ) );
                    MouseOverTerrain.Tile.Alignment = MouseOverTerrain.Vertex.Normal;
                    MouseOverTerrain.Vertex.Alignment = new sXY_int(MouseOverTerrain.Tile.Normal.X + 1, MouseOverTerrain.Tile.Normal.Y + 1);
                    MouseOverTerrain.Triangle = Map.GetTerrainTri(MouseOverTerrain.Pos.Horizontal);
                    XY_dbl.X = MouseOverTerrain.Pos.Horizontal.X - MouseOverTerrain.Vertex.Normal.X * App.TerrainGridSpacing;
                    XY_dbl.Y = MouseOverTerrain.Pos.Horizontal.Y - MouseOverTerrain.Vertex.Normal.Y * App.TerrainGridSpacing;
                    if ( Math.Abs(XY_dbl.Y) <= Math.Abs(XY_dbl.X) )
                    {
                        MouseOverTerrain.Side_IsV = false;
                        MouseOverTerrain.Side_Num.X = MouseOverTerrain.Tile.Normal.X;
                        MouseOverTerrain.Side_Num.Y = MouseOverTerrain.Vertex.Normal.Y;
                    }
                    else
                    {
                        MouseOverTerrain.Side_IsV = true;
                        MouseOverTerrain.Side_Num.X = MouseOverTerrain.Vertex.Normal.X;
                        MouseOverTerrain.Side_Num.Y = MouseOverTerrain.Tile.Normal.Y;
                    }
                    sXY_int SectorNum = Map.GetPosSectorNum(MouseOverTerrain.Pos.Horizontal);
                    clsUnit Unit = default(clsUnit);
                    clsUnitSectorConnection Connection = default(clsUnitSectorConnection);
                    foreach ( clsUnitSectorConnection tempLoopVar_Connection in Map.Sectors[SectorNum.X, SectorNum.Y].Units )
                    {
                        Connection = tempLoopVar_Connection;
                        Unit = Connection.Unit;
                        XY_dbl.X = Unit.Pos.Horizontal.X - MouseOverTerrain.Pos.Horizontal.X;
                        XY_dbl.Y = Unit.Pos.Horizontal.Y - MouseOverTerrain.Pos.Horizontal.Y;
                        Footprint = Unit.TypeBase.get_GetFootprintSelected(Unit.Rotation);
                        if ( Math.Abs(XY_dbl.X) <= Math.Max(Footprint.X / 2.0D, 0.5D) * App.TerrainGridSpacing
                             && Math.Abs(XY_dbl.Y) <= Math.Max(Footprint.Y / 2.0D, 0.5D) * App.TerrainGridSpacing )
                        {
                            MouseOverTerrain.Units.Add(Unit);
                        }
                    }

                    if ( MouseLeftDown != null )
                    {
                        if ( modTools.Tool == modTools.Tools.TerrainBrush )
                        {
                            Apply_Terrain();
                            if ( Program.frmMainInstance.cbxAutoTexSetHeight.Checked )
                            {
                                Apply_Height_Set(App.TerrainBrush, Program.frmMainInstance.HeightSetPalette[Program.frmMainInstance.tabHeightSetL.SelectedIndex]);
                            }
                        }
                        else if ( modTools.Tool == modTools.Tools.HeightSetBrush )
                        {
                            Apply_Height_Set(App.HeightBrush, Program.frmMainInstance.HeightSetPalette[Program.frmMainInstance.tabHeightSetL.SelectedIndex]);
                        }
                        else if ( modTools.Tool == modTools.Tools.TextureBrush )
                        {
                            Apply_Texture();
                        }
                        else if ( modTools.Tool == modTools.Tools.CliffTriangle )
                        {
                            Apply_CliffTriangle(false);
                        }
                        else if ( modTools.Tool == modTools.Tools.CliffBrush )
                        {
                            Apply_Cliff();
                        }
                        else if ( modTools.Tool == modTools.Tools.CliffRemove )
                        {
                            Apply_Cliff_Remove();
                        }
                        else if ( modTools.Tool == modTools.Tools.RoadPlace )
                        {
                            Apply_Road();
                        }
                        else if ( modTools.Tool == modTools.Tools.RoadRemove )
                        {
                            Apply_Road_Remove();
                        }
                    }
                    if ( MouseRightDown != null )
                    {
                        if ( modTools.Tool == modTools.Tools.HeightSetBrush )
                        {
                            if ( MouseLeftDown == null )
                            {
                                Apply_Height_Set(App.HeightBrush, Program.frmMainInstance.HeightSetPalette[Program.frmMainInstance.tabHeightSetR.SelectedIndex]);
                            }
                        }
                        else if ( modTools.Tool == modTools.Tools.CliffTriangle )
                        {
                            Apply_CliffTriangle(true);
                        }
                    }
                }
            }
            MapViewControl.Pos_Display_Update();
            MapViewControl.DrawViewLater();
        }

        public clsMouseOver.clsOverTerrain GetMouseOverTerrain()
        {
            if ( MouseOver == null )
            {
                return null;
            }
            else
            {
                return MouseOver.OverTerrain;
            }
        }

        public clsMouseDown.clsOverTerrain GetMouseLeftDownOverTerrain()
        {
            if ( MouseLeftDown == null )
            {
                return null;
            }
            else
            {
                return MouseLeftDown.OverTerrain;
            }
        }

        public clsMouseDown.clsOverTerrain GetMouseRightDownOverTerrain()
        {
            if ( MouseRightDown == null )
            {
                return null;
            }
            else
            {
                return MouseRightDown.OverTerrain;
            }
        }

        public clsMouseDown.clsOverMinimap GetMouseLeftDownOverMinimap()
        {
            if ( MouseLeftDown == null )
            {
                return null;
            }
            else
            {
                return MouseLeftDown.OverMinimap;
            }
        }

        public clsMouseDown.clsOverMinimap GetMouseRightDownOverMinimap()
        {
            if ( MouseRightDown == null )
            {
                return null;
            }
            else
            {
                return MouseRightDown.OverMinimap;
            }
        }

        public class clsMouseOver
        {
            public sXY_int ScreenPos;

            public class clsOverTerrain
            {
                public sWorldPos Pos;
                public SimpleClassList<clsUnit> Units = new SimpleClassList<clsUnit>();
                public clsBrush.sPosNum Tile;
                public clsBrush.sPosNum Vertex;
                public bool Triangle;
                public sXY_int Side_Num;
                public bool Side_IsV;
            }

            public clsOverTerrain OverTerrain;
        }

        public class clsMouseDown
        {
            public class clsOverTerrain
            {
                public sWorldPos DownPos;
            }

            public clsOverTerrain OverTerrain;

            public class clsOverMinimap
            {
                public sXY_int DownPos;
            }

            public clsOverMinimap OverMinimap;
        }

        public clsMouseOver MouseOver;

        public clsMouseDown MouseLeftDown;
        public clsMouseDown MouseRightDown;

        public bool IsViewPosOverMinimap(sXY_int Pos)
        {
            if ( Pos.X >= 0 & Pos.X < Map.Terrain.TileSize.X / Tiles_Per_Minimap_Pixel
                 & Pos.Y >= 0 & Pos.Y < Map.Terrain.TileSize.Y / Tiles_Per_Minimap_Pixel )
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Apply_Terrain()
        {
            clsMouseOver.clsOverTerrain MouseOverTerrain = GetMouseOverTerrain();

            if ( MouseOverTerrain == null )
            {
                return;
            }

            clsApplyVertexTerrain ApplyVertexTerrain = new clsApplyVertexTerrain();
            ApplyVertexTerrain.Map = Map;
            ApplyVertexTerrain.VertexTerrain = App.SelectedTerrain;
            App.TerrainBrush.PerformActionMapVertices(ApplyVertexTerrain, MouseOverTerrain.Vertex);

            Map.Update();

            MapViewControl.DrawViewLater();
        }

        public void Apply_Road()
        {
            clsMouseOver.clsOverTerrain MouseOverTerrain = GetMouseOverTerrain();

            if ( MouseOverTerrain == null )
            {
                return;
            }

            sXY_int Side_Num = MouseOverTerrain.Side_Num;
            sXY_int TileNum = new sXY_int();

            if ( MouseOverTerrain.Side_IsV )
            {
                if ( Map.Terrain.SideV[Side_Num.X, Side_Num.Y].Road != App.SelectedRoad )
                {
                    Map.Terrain.SideV[Side_Num.X, Side_Num.Y].Road = App.SelectedRoad;

                    if ( Side_Num.X > 0 )
                    {
                        TileNum.X = Side_Num.X - 1;
                        TileNum.Y = Side_Num.Y;
                        Map.AutoTextureChanges.TileChanged(TileNum);
                        Map.SectorGraphicsChanges.TileChanged(TileNum);
                        Map.SectorTerrainUndoChanges.TileChanged(TileNum);
                    }
                    if ( Side_Num.X < Map.Terrain.TileSize.X )
                    {
                        TileNum = Side_Num;
                        Map.AutoTextureChanges.TileChanged(TileNum);
                        Map.SectorGraphicsChanges.TileChanged(TileNum);
                        Map.SectorTerrainUndoChanges.TileChanged(TileNum);
                    }

                    Map.Update();

                    Map.UndoStepCreate("Road Side");

                    MapViewControl.DrawViewLater();
                }
            }
            else
            {
                if ( Map.Terrain.SideH[Side_Num.X, Side_Num.Y].Road != App.SelectedRoad )
                {
                    Map.Terrain.SideH[Side_Num.X, Side_Num.Y].Road = App.SelectedRoad;

                    if ( Side_Num.Y > 0 )
                    {
                        TileNum.X = Side_Num.X;
                        TileNum.Y = Side_Num.Y - 1;
                        Map.AutoTextureChanges.TileChanged(TileNum);
                        Map.SectorGraphicsChanges.TileChanged(TileNum);
                        Map.SectorTerrainUndoChanges.TileChanged(TileNum);
                    }
                    if ( Side_Num.Y < Map.Terrain.TileSize.X )
                    {
                        TileNum = Side_Num;
                        Map.AutoTextureChanges.TileChanged(TileNum);
                        Map.SectorGraphicsChanges.TileChanged(TileNum);
                        Map.SectorTerrainUndoChanges.TileChanged(TileNum);
                    }

                    Map.Update();

                    Map.UndoStepCreate("Road Side");

                    MapViewControl.DrawViewLater();
                }
            }
        }

        public void Apply_Road_Line_Selection()
        {
            clsMouseOver.clsOverTerrain MouseOverTerrian = GetMouseOverTerrain();

            if ( MouseOverTerrian == null )
            {
                return;
            }

            int Num = 0;
            int A = 0;
            int B = 0;
            sXY_int Tile = MouseOverTerrian.Tile.Normal;
            sXY_int SideNum = new sXY_int();

            if ( Map.Selected_Tile_A != null )
            {
                if ( Tile.X == Map.Selected_Tile_A.X )
                {
                    if ( Tile.Y <= Map.Selected_Tile_A.Y )
                    {
                        A = Tile.Y;
                        B = Map.Selected_Tile_A.Y;
                    }
                    else
                    {
                        A = Map.Selected_Tile_A.Y;
                        B = Tile.Y;
                    }
                    for ( Num = A + 1; Num <= B; Num++ )
                    {
                        Map.Terrain.SideH[Map.Selected_Tile_A.X, Num].Road = App.SelectedRoad;
                        SideNum.X = Map.Selected_Tile_A.X;
                        SideNum.Y = Num;
                        Map.AutoTextureChanges.SideHChanged(SideNum);
                        Map.SectorGraphicsChanges.SideHChanged(SideNum);
                        Map.SectorTerrainUndoChanges.SideHChanged(SideNum);
                    }

                    Map.Update();

                    Map.UndoStepCreate("Road Line");

                    Map.Selected_Tile_A = null;
                    MapViewControl.DrawViewLater();
                }
                else if ( Tile.Y == Map.Selected_Tile_A.Y )
                {
                    if ( Tile.X <= Map.Selected_Tile_A.X )
                    {
                        A = Tile.X;
                        B = Map.Selected_Tile_A.X;
                    }
                    else
                    {
                        A = Map.Selected_Tile_A.X;
                        B = Tile.X;
                    }
                    for ( Num = A + 1; Num <= B; Num++ )
                    {
                        Map.Terrain.SideV[Num, Map.Selected_Tile_A.Y].Road = App.SelectedRoad;
                        SideNum.X = Num;
                        SideNum.Y = Map.Selected_Tile_A.Y;
                        Map.AutoTextureChanges.SideVChanged(SideNum);
                        Map.SectorGraphicsChanges.SideVChanged(SideNum);
                        Map.SectorTerrainUndoChanges.SideVChanged(SideNum);
                    }

                    Map.Update();

                    Map.UndoStepCreate("Road Line");

                    Map.Selected_Tile_A = null;
                    MapViewControl.DrawViewLater();
                }
                else
                {
                }
            }
            else
            {
                Map.Selected_Tile_A = new clsXY_int(Tile);
            }
        }

        public void Apply_Terrain_Fill(enumFillCliffAction CliffAction, bool Inside)
        {
            clsMouseOver.clsOverTerrain MouseOverTerrain = GetMouseOverTerrain();

            if ( MouseOverTerrain == null )
            {
                return;
            }

            Terrain FillType = default(Terrain);
            Terrain ReplaceType = default(Terrain);
            sXY_int StartVertex = MouseOverTerrain.Vertex.Normal;

            FillType = App.SelectedTerrain;
            ReplaceType = Map.Terrain.Vertices[StartVertex.X, StartVertex.Y].Terrain;
            if ( FillType == ReplaceType )
            {
                return; //otherwise will cause endless loop
            }

            int A = 0;
            sXY_int[] SourceOfFill = new sXY_int[524289];
            int SourceOfFillCount = 0;
            int SourceOfFillNum = 0;
            int MoveCount = 0;
            int RemainingCount = 0;
            int MoveOffset = 0;
            sXY_int CurrentSource = new sXY_int();
            sXY_int NextSource = new sXY_int();
            bool StopForCliff = default(bool);
            bool StopForEdge = default(bool);

            SourceOfFill[0] = StartVertex;
            SourceOfFillCount = 1;
            SourceOfFillNum = 0;
            while ( SourceOfFillNum < SourceOfFillCount )
            {
                CurrentSource = SourceOfFill[SourceOfFillNum];

                if ( CliffAction == enumFillCliffAction.StopBefore )
                {
                    StopForCliff = Map.VertexIsCliffEdge(CurrentSource);
                }
                else
                {
                    StopForCliff = false;
                }
                StopForEdge = false;
                if ( Inside )
                {
                    if ( CurrentSource.X > 0 )
                    {
                        if ( CurrentSource.Y > 0 )
                        {
                            if ( Map.Terrain.Vertices[CurrentSource.X - 1, CurrentSource.Y - 1].Terrain != ReplaceType
                                 && Map.Terrain.Vertices[CurrentSource.X - 1, CurrentSource.Y - 1].Terrain != FillType )
                            {
                                StopForEdge = true;
                            }
                        }
                        if ( Map.Terrain.Vertices[CurrentSource.X - 1, CurrentSource.Y].Terrain != ReplaceType
                             && Map.Terrain.Vertices[CurrentSource.X - 1, CurrentSource.Y].Terrain != FillType )
                        {
                            StopForEdge = true;
                        }
                        if ( CurrentSource.Y < Map.Terrain.TileSize.Y )
                        {
                            if ( Map.Terrain.Vertices[CurrentSource.X - 1, CurrentSource.Y + 1].Terrain != ReplaceType
                                 && Map.Terrain.Vertices[CurrentSource.X - 1, CurrentSource.Y + 1].Terrain != FillType )
                            {
                                StopForEdge = true;
                            }
                        }
                    }
                    if ( CurrentSource.Y > 0 )
                    {
                        if ( Map.Terrain.Vertices[CurrentSource.X, CurrentSource.Y - 1].Terrain != ReplaceType
                             && Map.Terrain.Vertices[CurrentSource.X, CurrentSource.Y - 1].Terrain != FillType )
                        {
                            StopForEdge = true;
                        }
                    }
                    if ( CurrentSource.X < Map.Terrain.TileSize.X )
                    {
                        if ( CurrentSource.Y > 0 )
                        {
                            if ( Map.Terrain.Vertices[CurrentSource.X + 1, CurrentSource.Y - 1].Terrain != ReplaceType
                                 && Map.Terrain.Vertices[CurrentSource.X + 1, CurrentSource.Y - 1].Terrain != FillType )
                            {
                                StopForEdge = true;
                            }
                        }
                        if ( Map.Terrain.Vertices[CurrentSource.X + 1, CurrentSource.Y].Terrain != ReplaceType
                             && Map.Terrain.Vertices[CurrentSource.X + 1, CurrentSource.Y].Terrain != FillType )
                        {
                            StopForEdge = true;
                        }
                        if ( CurrentSource.Y < Map.Terrain.TileSize.Y )
                        {
                            if ( Map.Terrain.Vertices[CurrentSource.X + 1, CurrentSource.Y + 1].Terrain != ReplaceType
                                 && Map.Terrain.Vertices[CurrentSource.X + 1, CurrentSource.Y + 1].Terrain != FillType )
                            {
                                StopForEdge = true;
                            }
                        }
                    }
                    if ( CurrentSource.Y < Map.Terrain.TileSize.Y )
                    {
                        if ( Map.Terrain.Vertices[CurrentSource.X, CurrentSource.Y + 1].Terrain != ReplaceType
                             && Map.Terrain.Vertices[CurrentSource.X, CurrentSource.Y + 1].Terrain != FillType )
                        {
                            StopForEdge = true;
                        }
                    }
                }

                if ( !(StopForCliff || StopForEdge) )
                {
                    if ( Map.Terrain.Vertices[CurrentSource.X, CurrentSource.Y].Terrain == ReplaceType )
                    {
                        Map.Terrain.Vertices[CurrentSource.X, CurrentSource.Y].Terrain = FillType;
                        Map.SectorGraphicsChanges.VertexChanged(CurrentSource);
                        Map.SectorTerrainUndoChanges.VertexChanged(CurrentSource);
                        Map.AutoTextureChanges.VertexChanged(CurrentSource);

                        NextSource.X = CurrentSource.X + 1;
                        NextSource.Y = CurrentSource.Y;
                        if ( NextSource.X >= 0 & NextSource.X <= Map.Terrain.TileSize.X
                             & NextSource.Y >= 0 & NextSource.Y <= Map.Terrain.TileSize.Y )
                        {
                            if ( CliffAction == enumFillCliffAction.StopAfter )
                            {
                                StopForCliff = Map.SideHIsCliffOnBothSides(new sXY_int(CurrentSource.X, CurrentSource.Y));
                            }
                            else
                            {
                                StopForCliff = false;
                            }
                            if ( !StopForCliff )
                            {
                                if ( Map.Terrain.Vertices[NextSource.X, NextSource.Y].Terrain == ReplaceType )
                                {
                                    if ( SourceOfFill.GetUpperBound(0) < SourceOfFillCount )
                                    {
                                        Array.Resize(ref SourceOfFill, SourceOfFillCount * 2 + 1 + 1);
                                    }
                                    SourceOfFill[SourceOfFillCount] = NextSource;
                                    SourceOfFillCount++;
                                }
                            }
                        }

                        NextSource.X = CurrentSource.X - 1;
                        NextSource.Y = CurrentSource.Y;
                        if ( NextSource.X >= 0 & NextSource.X <= Map.Terrain.TileSize.X
                             & NextSource.Y >= 0 & NextSource.Y <= Map.Terrain.TileSize.Y )
                        {
                            if ( CliffAction == enumFillCliffAction.StopAfter )
                            {
                                StopForCliff = Map.SideHIsCliffOnBothSides(new sXY_int(CurrentSource.X - 1, CurrentSource.Y));
                            }
                            else
                            {
                                StopForCliff = false;
                            }
                            if ( !StopForCliff )
                            {
                                if ( Map.Terrain.Vertices[NextSource.X, NextSource.Y].Terrain == ReplaceType )
                                {
                                    if ( SourceOfFill.GetUpperBound(0) < SourceOfFillCount )
                                    {
                                        Array.Resize(ref SourceOfFill, SourceOfFillCount * 2 + 1 + 1);
                                    }
                                    SourceOfFill[SourceOfFillCount] = NextSource;
                                    SourceOfFillCount++;
                                }
                            }
                        }

                        NextSource.X = CurrentSource.X;
                        NextSource.Y = CurrentSource.Y + 1;
                        if ( NextSource.X >= 0 & NextSource.X <= Map.Terrain.TileSize.X
                             & NextSource.Y >= 0 & NextSource.Y <= Map.Terrain.TileSize.Y )
                        {
                            if ( CliffAction == enumFillCliffAction.StopAfter )
                            {
                                StopForCliff = Map.SideVIsCliffOnBothSides(new sXY_int(CurrentSource.X, CurrentSource.Y));
                            }
                            else
                            {
                                StopForCliff = false;
                            }
                            if ( !StopForCliff )
                            {
                                if ( Map.Terrain.Vertices[NextSource.X, NextSource.Y].Terrain == ReplaceType )
                                {
                                    if ( SourceOfFill.GetUpperBound(0) < SourceOfFillCount )
                                    {
                                        Array.Resize(ref SourceOfFill, SourceOfFillCount * 2 + 1 + 1);
                                    }
                                    SourceOfFill[SourceOfFillCount] = NextSource;
                                    SourceOfFillCount++;
                                }
                            }
                        }

                        NextSource.X = CurrentSource.X;
                        NextSource.Y = CurrentSource.Y - 1;
                        if ( NextSource.X >= 0 & NextSource.X <= Map.Terrain.TileSize.X
                             & NextSource.Y >= 0 & NextSource.Y <= Map.Terrain.TileSize.Y )
                        {
                            if ( CliffAction == enumFillCliffAction.StopAfter )
                            {
                                StopForCliff = Map.SideVIsCliffOnBothSides(new sXY_int(CurrentSource.X, CurrentSource.Y - 1));
                            }
                            else
                            {
                                StopForCliff = false;
                            }
                            if ( !StopForCliff )
                            {
                                if ( Map.Terrain.Vertices[NextSource.X, NextSource.Y].Terrain == ReplaceType )
                                {
                                    if ( SourceOfFill.GetUpperBound(0) < SourceOfFillCount )
                                    {
                                        Array.Resize(ref SourceOfFill, SourceOfFillCount * 2 + 1 + 1);
                                    }
                                    SourceOfFill[SourceOfFillCount] = NextSource;
                                    SourceOfFillCount++;
                                }
                            }
                        }
                    }
                }

                SourceOfFillNum++;

                if ( SourceOfFillNum >= 131072 )
                {
                    RemainingCount = SourceOfFillCount - SourceOfFillNum;
                    MoveCount = Math.Min(SourceOfFillNum, RemainingCount);
                    MoveOffset = SourceOfFillCount - MoveCount;
                    for ( A = 0; A <= MoveCount - 1; A++ )
                    {
                        SourceOfFill[A] = SourceOfFill[MoveOffset + A];
                    }
                    SourceOfFillCount -= SourceOfFillNum;
                    SourceOfFillNum = 0;
                    if ( SourceOfFillCount * 3 < SourceOfFill.GetUpperBound(0) + 1 )
                    {
                        Array.Resize(ref SourceOfFill, SourceOfFillCount * 2 + 1 + 1);
                    }
                }
            }

            Map.Update();

            Map.UndoStepCreate("Ground Fill");

            MapViewControl.DrawViewLater();
        }

        public void Apply_Texture()
        {
            clsMouseOver.clsOverTerrain MouseOverTerrain = GetMouseOverTerrain();

            if ( MouseOverTerrain == null )
            {
                return;
            }

            clsApplyTexture ApplyTexture = new clsApplyTexture();
            ApplyTexture.Map = Map;
            ApplyTexture.TextureNum = App.SelectedTextureNum;
            ApplyTexture.SetTexture = Program.frmMainInstance.chkSetTexture.Checked;
            ApplyTexture.Orientation = App.TextureOrientation;
            ApplyTexture.RandomOrientation = Program.frmMainInstance.chkTextureOrientationRandomize.Checked;
            ApplyTexture.SetOrientation = Program.frmMainInstance.chkSetTextureOrientation.Checked;
            ApplyTexture.TerrainAction = Program.frmMainInstance.TextureTerrainAction;
            App.TextureBrush.PerformActionMapTiles(ApplyTexture, MouseOverTerrain.Tile);

            Map.Update();

            MapViewControl.DrawViewLater();
        }

        public void Apply_CliffTriangle(bool Remove)
        {
            clsMouseOver.clsOverTerrain MouseOverTerrain = GetMouseOverTerrain();

            if ( MouseOverTerrain == null )
            {
                return;
            }

            if ( Remove )
            {
                clsApplyCliffTriangleRemove ApplyCliffTriangleRemove = new clsApplyCliffTriangleRemove();
                ApplyCliffTriangleRemove.Map = Map;
                ApplyCliffTriangleRemove.PosNum = MouseOverTerrain.Tile.Normal;
                ApplyCliffTriangleRemove.Triangle = MouseOverTerrain.Triangle;
                ApplyCliffTriangleRemove.ActionPerform();
            }
            else
            {
                clsApplyCliffTriangle ApplyCliffTriangle = new clsApplyCliffTriangle();
                ApplyCliffTriangle.Map = Map;
                ApplyCliffTriangle.PosNum = MouseOverTerrain.Tile.Normal;
                ApplyCliffTriangle.Triangle = MouseOverTerrain.Triangle;
                ApplyCliffTriangle.ActionPerform();
            }

            Map.Update();

            MapViewControl.DrawViewLater();
        }

        public void Apply_Cliff()
        {
            clsMouseOver.clsOverTerrain MouseOverTerrain = GetMouseOverTerrain();

            if ( MouseOverTerrain == null )
            {
                return;
            }

            clsApplyCliff ApplyCliff = new clsApplyCliff();
            ApplyCliff.Map = Map;
            double Angle = 0;
            if ( !IOUtil.InvariantParse(Program.frmMainInstance.txtAutoCliffSlope.Text, ref Angle) )
            {
                return;
            }
            ApplyCliff.Angle = MathUtil.Clamp_dbl(Angle * MathUtil.RadOf1Deg, 0.0D, MathUtil.RadOf90Deg);
            ApplyCliff.SetTris = Program.frmMainInstance.cbxCliffTris.Checked;
            App.CliffBrush.PerformActionMapTiles(ApplyCliff, MouseOverTerrain.Tile);

            Map.Update();

            MapViewControl.DrawViewLater();
        }

        public void Apply_Cliff_Remove()
        {
            clsMouseOver.clsOverTerrain MouseOverTerrain = GetMouseOverTerrain();

            if ( MouseOverTerrain == null )
            {
                return;
            }

            clsApplyCliffRemove ApplyCliffRemove = new clsApplyCliffRemove();
            ApplyCliffRemove.Map = Map;
            App.CliffBrush.PerformActionMapTiles(ApplyCliffRemove, MouseOverTerrain.Tile);

            Map.Update();

            MapViewControl.DrawViewLater();
        }

        public void Apply_Road_Remove()
        {
            clsMouseOver.clsOverTerrain MouseOverTerrain = GetMouseOverTerrain();

            if ( MouseOverTerrain == null )
            {
                return;
            }

            clsApplyRoadRemove ApplyRoadRemove = new clsApplyRoadRemove();
            ApplyRoadRemove.Map = Map;
            App.CliffBrush.PerformActionMapTiles(ApplyRoadRemove, MouseOverTerrain.Tile);

            Map.Update();

            MapViewControl.DrawViewLater();
        }

        public void Apply_Texture_Clockwise()
        {
            clsMouseOver.clsOverTerrain MouseOverTerrain = GetMouseOverTerrain();

            if ( MouseOverTerrain == null )
            {
                return;
            }

            sXY_int Tile = MouseOverTerrain.Tile.Normal;

            Map.Terrain.Tiles[Tile.X, Tile.Y].Texture.Orientation.RotateClockwise();
            Map.TileTextureChangeTerrainAction(Tile, Program.frmMainInstance.TextureTerrainAction);

            Map.SectorGraphicsChanges.TileChanged(Tile);
            Map.SectorTerrainUndoChanges.TileChanged(Tile);

            Map.Update();

            Map.UndoStepCreate("Texture Rotate");

            MapViewControl.DrawViewLater();
        }

        public void Apply_Texture_CounterClockwise()
        {
            clsMouseOver.clsOverTerrain MouseOverTerrain = GetMouseOverTerrain();

            if ( MouseOverTerrain == null )
            {
                return;
            }

            sXY_int Tile = MouseOverTerrain.Tile.Normal;

            Map.Terrain.Tiles[Tile.X, Tile.Y].Texture.Orientation.RotateAntiClockwise();
            Map.TileTextureChangeTerrainAction(Tile, Program.frmMainInstance.TextureTerrainAction);

            Map.SectorGraphicsChanges.TileChanged(Tile);
            Map.SectorTerrainUndoChanges.TileChanged(Tile);

            Map.Update();

            Map.UndoStepCreate("Texture Rotate");

            MapViewControl.DrawViewLater();
        }

        public void Apply_Texture_FlipX()
        {
            clsMouseOver.clsOverTerrain MouseOverTerrain = GetMouseOverTerrain();

            if ( MouseOverTerrain == null )
            {
                return;
            }

            sXY_int Tile = MouseOverTerrain.Tile.Normal;

            Map.Terrain.Tiles[Tile.X, Tile.Y].Texture.Orientation.ResultXFlip = !Map.Terrain.Tiles[Tile.X, Tile.Y].Texture.Orientation.ResultXFlip;
            Map.TileTextureChangeTerrainAction(Tile, Program.frmMainInstance.TextureTerrainAction);

            Map.SectorGraphicsChanges.TileChanged(Tile);
            Map.SectorTerrainUndoChanges.TileChanged(Tile);

            Map.Update();

            Map.UndoStepCreate("Texture Rotate");

            MapViewControl.DrawViewLater();
        }

        public void Apply_Tri_Flip()
        {
            clsMouseOver.clsOverTerrain MouseOverTerrain = GetMouseOverTerrain();

            if ( MouseOverTerrain == null )
            {
                return;
            }

            sXY_int Tile = MouseOverTerrain.Tile.Normal;

            Map.Terrain.Tiles[Tile.X, Tile.Y].Tri = !Map.Terrain.Tiles[Tile.X, Tile.Y].Tri;

            Map.SectorGraphicsChanges.TileChanged(Tile);
            Map.SectorTerrainUndoChanges.TileChanged(Tile);

            Map.Update();

            Map.UndoStepCreate("Triangle Flip");

            MapViewControl.DrawViewLater();
        }

        public void Apply_HeightSmoothing(double Ratio)
        {
            clsMouseOver.clsOverTerrain MouseOverTerrain = GetMouseOverTerrain();

            if ( MouseOverTerrain == null )
            {
                return;
            }

            clsApplyHeightSmoothing ApplyHeightSmoothing = new clsApplyHeightSmoothing();
            ApplyHeightSmoothing.Map = Map;
            ApplyHeightSmoothing.Ratio = Ratio;
            int Radius = (int)(Math.Ceiling(App.HeightBrush.Radius));
            sXY_int PosNum = App.HeightBrush.GetPosNum(MouseOverTerrain.Vertex);
            ApplyHeightSmoothing.Offset.X = MathUtil.Clamp_int(PosNum.X - Radius, 0, Map.Terrain.TileSize.X);
            ApplyHeightSmoothing.Offset.Y = MathUtil.Clamp_int(PosNum.Y - Radius, 0, Map.Terrain.TileSize.Y);
            sXY_int PosEnd = new sXY_int();
            PosEnd.X = MathUtil.Clamp_int(PosNum.X + Radius, 0, Map.Terrain.TileSize.X);
            PosEnd.Y = MathUtil.Clamp_int(PosNum.Y + Radius, 0, Map.Terrain.TileSize.Y);
            ApplyHeightSmoothing.AreaTileSize.X = PosEnd.X - ApplyHeightSmoothing.Offset.X;
            ApplyHeightSmoothing.AreaTileSize.Y = PosEnd.Y - ApplyHeightSmoothing.Offset.Y;
            ApplyHeightSmoothing.Start();
            App.HeightBrush.PerformActionMapVertices(ApplyHeightSmoothing, MouseOverTerrain.Vertex);
            ApplyHeightSmoothing.Finish();

            Map.Update();

            MapViewControl.DrawViewLater();
        }

        public void Apply_Height_Change(double Rate)
        {
            clsMouseOver.clsOverTerrain MouseOverTerrain = GetMouseOverTerrain();

            if ( MouseOverTerrain == null )
            {
                return;
            }

            clsApplyHeightChange ApplyHeightChange = new clsApplyHeightChange();
            ApplyHeightChange.Map = Map;
            ApplyHeightChange.Rate = Rate;
            ApplyHeightChange.UseEffect = Program.frmMainInstance.cbxHeightChangeFade.Checked;
            App.HeightBrush.PerformActionMapVertices(ApplyHeightChange, MouseOverTerrain.Vertex);

            Map.Update();

            MapViewControl.DrawViewLater();
        }

        public void Apply_Height_Set(clsBrush Brush, byte Height)
        {
            clsMouseOver.clsOverTerrain MouseOverTerrain = GetMouseOverTerrain();

            if ( MouseOverTerrain == null )
            {
                return;
            }

            clsApplyHeightSet ApplyHeightSet = new clsApplyHeightSet();
            ApplyHeightSet.Map = Map;
            ApplyHeightSet.Height = Height;
            Brush.PerformActionMapVertices(ApplyHeightSet, MouseOverTerrain.Vertex);

            Map.Update();

            MapViewControl.DrawViewLater();
        }

        public void Apply_Gateway()
        {
            clsMouseOver.clsOverTerrain MouseOverTerrain = GetMouseOverTerrain();

            if ( MouseOverTerrain == null )
            {
                return;
            }

            sXY_int Tile = MouseOverTerrain.Tile.Normal;

            if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.Gateway_Delete) )
            {
                int A = 0;
                sXY_int Low = new sXY_int();
                sXY_int High = new sXY_int();
                A = 0;
                while ( A < Map.Gateways.Count )
                {
                    MathUtil.ReorderXY(Map.Gateways[A].PosA, Map.Gateways[A].PosB, Low, High);
                    if ( Low.X <= Tile.X
                         & High.X >= Tile.X
                         & Low.Y <= Tile.Y
                         & High.Y >= Tile.Y )
                    {
                        Map.GatewayRemoveStoreChange(A);
                        Map.UndoStepCreate("Gateway Delete");
                        Map.MinimapMakeLater();
                        MapViewControl.DrawViewLater();
                        break;
                    }
                    A++;
                }
            }
            else
            {
                if ( Map.Selected_Tile_A == null )
                {
                    Map.Selected_Tile_A = new clsXY_int(Tile);
                    MapViewControl.DrawViewLater();
                }
                else if ( Tile.X == Map.Selected_Tile_A.X | Tile.Y == Map.Selected_Tile_A.Y )
                {
                    if ( Map.GatewayCreateStoreChange(Map.Selected_Tile_A.XY, Tile) != null )
                    {
                        Map.UndoStepCreate("Gateway Place");
                        Map.Selected_Tile_A = null;
                        Map.Selected_Tile_B = null;
                        Map.MinimapMakeLater();
                        MapViewControl.DrawViewLater();
                    }
                }
            }
        }

        public void MouseDown(MouseEventArgs e)
        {
            sXY_int ScreenPos = new sXY_int();

            Map.SuppressMinimap = true;

            ScreenPos.X = e.X;
            ScreenPos.Y = e.Y;
            if ( e.Button == MouseButtons.Left )
            {
                MouseLeftDown = new clsMouseDown();
                if ( IsViewPosOverMinimap(ScreenPos) )
                {
                    MouseLeftDown.OverMinimap = new clsMouseDown.clsOverMinimap();
                    MouseLeftDown.OverMinimap.DownPos = ScreenPos;
                    sXY_int Pos = new sXY_int((int)(Conversion.Int(ScreenPos.X * Tiles_Per_Minimap_Pixel)),
                        (int)Conversion.Int(ScreenPos.Y * Tiles_Per_Minimap_Pixel));
                    Map.TileNumClampToMap(Pos);
                    LookAtTile(Pos);
                }
                else
                {
                    clsMouseOver.clsOverTerrain MouseOverTerrain = GetMouseOverTerrain();
                    if ( MouseOverTerrain != null )
                    {
                        MouseLeftDown.OverTerrain = new clsMouseDown.clsOverTerrain();
                        MouseLeftDown.OverTerrain.DownPos = MouseOverTerrain.Pos;
                        if ( modTools.Tool == modTools.Tools.ObjectSelect )
                        {
                            if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.Picker) )
                            {
                                if ( MouseOverTerrain.Units.Count > 0 )
                                {
                                    if ( MouseOverTerrain.Units.Count == 1 )
                                    {
                                        Program.frmMainInstance.ObjectPicker(MouseOverTerrain.Units[0].TypeBase);
                                    }
                                    else
                                    {
                                        MapViewControl.ListSelectBegin(true);
                                    }
                                }
                            }
                            else if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ScriptPosition) )
                            {
                                clsScriptPosition NewPosition = clsScriptPosition.Create(Map);
                                if ( NewPosition != null )
                                {
                                    NewPosition.PosX = MouseLeftDown.OverTerrain.DownPos.Horizontal.X;
                                    NewPosition.PosY = MouseLeftDown.OverTerrain.DownPos.Horizontal.Y;
                                    Program.frmMainInstance.ScriptMarkerLists_Update();
                                }
                            }
                            else
                            {
                                if ( !KeyboardManager.KeyboardProfile.Active(KeyboardManager.UnitMultiselect) )
                                {
                                    Map.SelectedUnits.Clear();
                                }
                                Program.frmMainInstance.SelectedObject_Changed();
                                Map.Unit_Selected_Area_VertexA = new clsXY_int(MouseOverTerrain.Vertex.Normal);
                                MapViewControl.DrawViewLater();
                            }
                        }
                        else if ( modTools.Tool == modTools.Tools.TerrainBrush )
                        {
                            if ( Map.Tileset != null )
                            {
                                if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.Picker) )
                                {
                                    Program.frmMainInstance.TerrainPicker();
                                }
                                else
                                {
                                    Apply_Terrain();
                                    if ( Program.frmMainInstance.cbxAutoTexSetHeight.Checked )
                                    {
                                        Apply_Height_Set(App.TerrainBrush,
                                            Program.frmMainInstance.HeightSetPalette[Program.frmMainInstance.tabHeightSetL.SelectedIndex]);
                                    }
                                }
                            }
                        }
                        else if ( modTools.Tool == modTools.Tools.HeightSetBrush )
                        {
                            if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.Picker) )
                            {
                                Program.frmMainInstance.HeightPickerL();
                            }
                            else
                            {
                                Apply_Height_Set(App.HeightBrush, Program.frmMainInstance.HeightSetPalette[Program.frmMainInstance.tabHeightSetL.SelectedIndex]);
                            }
                        }
                        else if ( modTools.Tool == modTools.Tools.TextureBrush )
                        {
                            if ( Map.Tileset != null )
                            {
                                if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.Picker) )
                                {
                                    Program.frmMainInstance.TexturePicker();
                                }
                                else
                                {
                                    Apply_Texture();
                                }
                            }
                        }
                        else if ( modTools.Tool == modTools.Tools.CliffTriangle )
                        {
                            Apply_CliffTriangle(false);
                        }
                        else if ( modTools.Tool == modTools.Tools.CliffBrush )
                        {
                            Apply_Cliff();
                        }
                        else if ( modTools.Tool == modTools.Tools.CliffRemove )
                        {
                            Apply_Cliff_Remove();
                        }
                        else if ( modTools.Tool == modTools.Tools.TerrainFill )
                        {
                            if ( Map.Tileset != null )
                            {
                                if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.Picker) )
                                {
                                    Program.frmMainInstance.TerrainPicker();
                                }
                                else
                                {
                                    Apply_Terrain_Fill(Program.frmMainInstance.FillCliffAction, Program.frmMainInstance.cbxFillInside.Checked);
                                    MapViewControl.DrawViewLater();
                                }
                            }
                        }
                        else if ( modTools.Tool == modTools.Tools.RoadPlace )
                        {
                            if ( Map.Tileset != null )
                            {
                                Apply_Road();
                            }
                        }
                        else if ( modTools.Tool == modTools.Tools.RoadLines )
                        {
                            if ( Map.Tileset != null )
                            {
                                Apply_Road_Line_Selection();
                            }
                        }
                        else if ( modTools.Tool == modTools.Tools.RoadRemove )
                        {
                            Apply_Road_Remove();
                        }
                        else if ( modTools.Tool == modTools.Tools.ObjectPlace )
                        {
                            if ( Program.frmMainInstance.SingleSelectedObjectTypeBase != null && Map.SelectedUnitGroup != null )
                            {
                                clsUnitCreate objectCreator = new clsUnitCreate();
                                Map.SetObjectCreatorDefaults(objectCreator);
                                objectCreator.Horizontal = MouseOverTerrain.Pos.Horizontal;
                                objectCreator.Perform();
                                Map.UndoStepCreate("Place Object");
                                Map.Update();
                                Map.MinimapMakeLater();
                                MapViewControl.DrawViewLater();
                            }
                        }
                        else if ( modTools.Tool == modTools.Tools.ObjectLines )
                        {
                            ApplyObjectLine();
                        }
                        else if ( modTools.Tool == modTools.Tools.TerrainSelect )
                        {
                            if ( Map.Selected_Area_VertexA == null )
                            {
                                Map.Selected_Area_VertexA = new clsXY_int(MouseOverTerrain.Vertex.Normal);
                                MapViewControl.DrawViewLater();
                            }
                            else if ( Map.Selected_Area_VertexB == null )
                            {
                                Map.Selected_Area_VertexB = new clsXY_int(MouseOverTerrain.Vertex.Normal);
                                MapViewControl.DrawViewLater();
                            }
                            else
                            {
                                Map.Selected_Area_VertexA = null;
                                Map.Selected_Area_VertexB = null;
                                MapViewControl.DrawViewLater();
                            }
                        }
                        else if ( modTools.Tool == modTools.Tools.Gateways )
                        {
                            Apply_Gateway();
                        }
                    }
                    else if ( modTools.Tool == modTools.Tools.ObjectSelect )
                    {
                        Map.SelectedUnits.Clear();
                        Program.frmMainInstance.SelectedObject_Changed();
                    }
                }
            }
            else if ( e.Button == MouseButtons.Right )
            {
                MouseRightDown = new clsMouseDown();
                if ( IsViewPosOverMinimap(ScreenPos) )
                {
                    MouseRightDown.OverMinimap = new clsMouseDown.clsOverMinimap();
                    MouseRightDown.OverMinimap.DownPos = ScreenPos;
                }
                else
                {
                    clsMouseOver.clsOverTerrain MouseOverTerrain = GetMouseOverTerrain();
                    if ( MouseOverTerrain != null )
                    {
                        MouseRightDown.OverTerrain = new clsMouseDown.clsOverTerrain();
                        MouseRightDown.OverTerrain.DownPos = MouseOverTerrain.Pos;
                    }
                }
                if ( modTools.Tool == modTools.Tools.RoadLines || modTools.Tool == modTools.Tools.ObjectLines )
                {
                    Map.Selected_Tile_A = null;
                    MapViewControl.DrawViewLater();
                }
                else if ( modTools.Tool == modTools.Tools.TerrainSelect )
                {
                    Map.Selected_Area_VertexA = null;
                    Map.Selected_Area_VertexB = null;
                    MapViewControl.DrawViewLater();
                }
                else if ( modTools.Tool == modTools.Tools.CliffTriangle )
                {
                    Apply_CliffTriangle(true);
                }
                else if ( modTools.Tool == modTools.Tools.Gateways )
                {
                    Map.Selected_Tile_A = null;
                    Map.Selected_Tile_B = null;
                    MapViewControl.DrawViewLater();
                }
                else if ( modTools.Tool == modTools.Tools.HeightSetBrush )
                {
                    if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.Picker) )
                    {
                        Program.frmMainInstance.HeightPickerR();
                    }
                    else
                    {
                        Apply_Height_Set(App.HeightBrush, Program.frmMainInstance.HeightSetPalette[Program.frmMainInstance.tabHeightSetR.SelectedIndex]);
                    }
                }
            }
        }

        public void TimedActions(double Zoom, double Move, double Pan, double Roll, double OrbitRate)
        {
            Position.XYZ_dbl XYZ_dbl = default(Position.XYZ_dbl);
            double PanRate = Pan * FieldOfViewY;
            Angles.AnglePY AnglePY = default(Angles.AnglePY);
            Matrix3DMath.Matrix3D matrixA = new Matrix3DMath.Matrix3D();
            Matrix3DMath.Matrix3D matrixB = new Matrix3DMath.Matrix3D();
            Position.XYZ_dbl ViewAngleChange = default(Position.XYZ_dbl);
            sXYZ_int ViewPosChangeXYZ = new sXYZ_int();
            bool AngleChanged = default(bool);

            Move *= FOVMultiplier * (MapViewControl.GLSize.X + MapViewControl.GLSize.Y) * Math.Max(Math.Abs(ViewPos.Y), 512.0D);

            if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewZoomIn) )
            {
                FOV_Scale_2E_Change(Convert.ToDouble(- Zoom));
            }
            if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewZoomOut) )
            {
                FOV_Scale_2E_Change(Zoom);
            }

            if ( App.ViewMoveType == enumView_Move_Type.Free )
            {
                ViewPosChangeXYZ.X = 0;
                ViewPosChangeXYZ.Y = 0;
                ViewPosChangeXYZ.Z = 0;
                if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewMoveForward) )
                {
                    Matrix3DMath.VectorForwardsRotationByMatrix(ViewAngleMatrix, Move, ref XYZ_dbl);
                    ViewPosChangeXYZ.Add_dbl(XYZ_dbl);
                }
                if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewMoveBackward) )
                {
                    Matrix3DMath.VectorBackwardsRotationByMatrix(ViewAngleMatrix, Move, ref XYZ_dbl);
                    ViewPosChangeXYZ.Add_dbl(XYZ_dbl);
                }
                if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewMoveLeft) )
                {
                    Matrix3DMath.VectorLeftRotationByMatrix(ViewAngleMatrix, Move, ref XYZ_dbl);
                    ViewPosChangeXYZ.Add_dbl(XYZ_dbl);
                }
                if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewMoveRight) )
                {
                    Matrix3DMath.VectorRightRotationByMatrix(ViewAngleMatrix, Move, ref XYZ_dbl);
                    ViewPosChangeXYZ.Add_dbl(XYZ_dbl);
                }
                if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewMoveUp) )
                {
                    Matrix3DMath.VectorUpRotationByMatrix(ViewAngleMatrix, Move, ref XYZ_dbl);
                    ViewPosChangeXYZ.Add_dbl(XYZ_dbl);
                }
                if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewMoveDown) )
                {
                    Matrix3DMath.VectorDownRotationByMatrix(ViewAngleMatrix, Move, ref XYZ_dbl);
                    ViewPosChangeXYZ.Add_dbl(XYZ_dbl);
                }

                ViewAngleChange.X = 0.0D;
                ViewAngleChange.Y = 0.0D;
                ViewAngleChange.Z = 0.0D;
                if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewLeft) )
                {
                    Matrix3DMath.VectorForwardsRotationByMatrix(ViewAngleMatrix, Roll, ref XYZ_dbl);
                    ViewAngleChange += XYZ_dbl;
                }
                if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewRight) )
                {
                    Matrix3DMath.VectorBackwardsRotationByMatrix(ViewAngleMatrix, Roll, ref XYZ_dbl);
                    ViewAngleChange += XYZ_dbl;
                }
                if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewBackward) )
                {
                    Matrix3DMath.VectorLeftRotationByMatrix(ViewAngleMatrix, PanRate, ref XYZ_dbl);
                    ViewAngleChange += XYZ_dbl;
                }
                if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewForward) )
                {
                    Matrix3DMath.VectorRightRotationByMatrix(ViewAngleMatrix, PanRate, ref XYZ_dbl);
                    ViewAngleChange += XYZ_dbl;
                }
                if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewRollLeft) )
                {
                    Matrix3DMath.VectorDownRotationByMatrix(ViewAngleMatrix, PanRate, ref XYZ_dbl);
                    ViewAngleChange += XYZ_dbl;
                }
                if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewRollRight) )
                {
                    Matrix3DMath.VectorUpRotationByMatrix(ViewAngleMatrix, PanRate, ref XYZ_dbl);
                    ViewAngleChange += XYZ_dbl;
                }

                if ( ViewPosChangeXYZ.X != 0.0D | ViewPosChangeXYZ.Y != 0.0D | ViewPosChangeXYZ.Z != 0.0D )
                {
                    ViewPosChange(ViewPosChangeXYZ);
                }
                //do rotation
                if ( ViewAngleChange.X != 0.0D | ViewAngleChange.Y != 0.0D | ViewAngleChange.Z != 0.0D )
                {
                    Matrix3DMath.VectorToPY(ViewAngleChange, ref AnglePY);
                    Matrix3DMath.MatrixSetToPY(matrixA, AnglePY);
                    Matrix3DMath.MatrixRotationAroundAxis(ViewAngleMatrix, matrixA, ViewAngleChange.GetMagnitude(), matrixB);
                    ViewAngleSet_Rotate(matrixB);
                }
            }
            else if ( App.ViewMoveType == enumView_Move_Type.RTS )
            {
                ViewPosChangeXYZ = new sXYZ_int();

                Matrix3DMath.MatrixToPY(ViewAngleMatrix, ref AnglePY);
                Matrix3DMath.MatrixSetToYAngle(matrixA, AnglePY.Yaw);
                if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewMoveForward) )
                {
                    Matrix3DMath.VectorForwardsRotationByMatrix(matrixA, Move, ref XYZ_dbl);
                    ViewPosChangeXYZ.Add_dbl(XYZ_dbl);
                }
                if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewMoveBackward) )
                {
                    Matrix3DMath.VectorBackwardsRotationByMatrix(matrixA, Move, ref XYZ_dbl);
                    ViewPosChangeXYZ.Add_dbl(XYZ_dbl);
                }
                if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewMoveLeft) )
                {
                    Matrix3DMath.VectorLeftRotationByMatrix(matrixA, Move, ref XYZ_dbl);
                    ViewPosChangeXYZ.Add_dbl(XYZ_dbl);
                }
                if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewMoveRight) )
                {
                    Matrix3DMath.VectorRightRotationByMatrix(matrixA, Move, ref XYZ_dbl);
                    ViewPosChangeXYZ.Add_dbl(XYZ_dbl);
                }
                if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewMoveUp) )
                {
                    ViewPosChangeXYZ.Y += (int)Move;
                }
                if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewMoveDown) )
                {
                    ViewPosChangeXYZ.Y -= (int)Move;
                }

                AngleChanged = false;

                if ( App.RTSOrbit )
                {
                    if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewForward) )
                    {
                        AnglePY.Pitch = MathUtil.Clamp_dbl(AnglePY.Pitch + OrbitRate, Convert.ToDouble(- MathUtil.RadOf90Deg + 0.03125D * MathUtil.RadOf1Deg),
                            MathUtil.RadOf90Deg - 0.03125D * MathUtil.RadOf1Deg);
                        AngleChanged = true;
                    }
                    if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewBackward) )
                    {
                        AnglePY.Pitch = MathUtil.Clamp_dbl(AnglePY.Pitch - OrbitRate, Convert.ToDouble(- MathUtil.RadOf90Deg + 0.03125D * MathUtil.RadOf1Deg),
                            MathUtil.RadOf90Deg - 0.03125D * MathUtil.RadOf1Deg);
                        AngleChanged = true;
                    }
                    if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewLeft) )
                    {
                        AnglePY.Yaw = MathUtil.AngleClamp(AnglePY.Yaw + OrbitRate);
                        AngleChanged = true;
                    }
                    if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewRight) )
                    {
                        AnglePY.Yaw = MathUtil.AngleClamp(AnglePY.Yaw - OrbitRate);
                        AngleChanged = true;
                    }
                }
                else
                {
                    if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewForward) )
                    {
                        AnglePY.Pitch = MathUtil.Clamp_dbl(AnglePY.Pitch - OrbitRate, Convert.ToDouble(- MathUtil.RadOf90Deg + 0.03125D * MathUtil.RadOf1Deg),
                            MathUtil.RadOf90Deg - 0.03125D * MathUtil.RadOf1Deg);
                        AngleChanged = true;
                    }
                    if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewBackward) )
                    {
                        AnglePY.Pitch = MathUtil.Clamp_dbl(AnglePY.Pitch + OrbitRate, Convert.ToDouble(- MathUtil.RadOf90Deg + 0.03125D * MathUtil.RadOf1Deg),
                            MathUtil.RadOf90Deg - 0.03125D * MathUtil.RadOf1Deg);
                        AngleChanged = true;
                    }
                    if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewLeft) )
                    {
                        AnglePY.Yaw = MathUtil.AngleClamp(AnglePY.Yaw - OrbitRate);
                        AngleChanged = true;
                    }
                    if ( KeyboardManager.KeyboardProfile.Active(KeyboardManager.ViewRight) )
                    {
                        AnglePY.Yaw = MathUtil.AngleClamp(AnglePY.Yaw + OrbitRate);
                        AngleChanged = true;
                    }
                }

                //Dim HeightChange As Double
                //HeightChange = Map.Terrain_Height_Get(view.View_Pos.X + ViewPosChange.X, view.View_Pos.Z + ViewPosChange.Z) - Map.Terrain_Height_Get(view.View_Pos.X, view.View_Pos.Z)

                //ViewPosChange.Y = ViewPosChange.Y + HeightChange

                if ( ViewPosChangeXYZ.X != 0.0D | ViewPosChangeXYZ.Y != 0.0D | ViewPosChangeXYZ.Z != 0.0D )
                {
                    ViewPosChange(ViewPosChangeXYZ);
                }
                if ( AngleChanged )
                {
                    Matrix3DMath.MatrixSetToPY(matrixA, AnglePY);
                    ViewAngleSet_Rotate(matrixA);
                }
            }
        }

        public void TimedTools()
        {
            if ( modTools.Tool == modTools.Tools.HeightSmoothBrush )
            {
                if ( GetMouseOverTerrain() != null )
                {
                    if ( GetMouseLeftDownOverTerrain() != null )
                    {
                        double dblTemp = 0;
                        if ( !IOUtil.InvariantParse(Program.frmMainInstance.txtSmoothRate.Text, ref dblTemp) )
                        {
                            return;
                        }
                        Apply_HeightSmoothing(MathUtil.Clamp_dbl(dblTemp * Program.frmMainInstance.tmrTool.Interval / 1000.0D, 0.0D, 1.0D));
                    }
                }
            }
            else if ( modTools.Tool == modTools.Tools.HeightChangeBrush )
            {
                if ( GetMouseOverTerrain() != null )
                {
                    double dblTemp = 0;
                    if ( !IOUtil.InvariantParse(Program.frmMainInstance.txtHeightChangeRate.Text, ref dblTemp) )
                    {
                        return;
                    }
                    if ( GetMouseLeftDownOverTerrain() != null )
                    {
                        Apply_Height_Change(MathUtil.Clamp_dbl(dblTemp, -255.0D, 255.0D));
                    }
                    else if ( GetMouseRightDownOverTerrain() != null )
                    {
                        Apply_Height_Change(MathUtil.Clamp_dbl(Convert.ToDouble(- dblTemp), -255.0D, 255.0D));
                    }
                }
            }
        }

        public void ApplyObjectLine()
        {
            if ( Program.frmMainInstance.SingleSelectedObjectTypeBase != null && Map.SelectedUnitGroup != null )
            {
                clsMouseOver.clsOverTerrain MouseOverTerrian = GetMouseOverTerrain();

                if ( MouseOverTerrian == null )
                {
                    return;
                }

                int Num = 0;
                int A = 0;
                int B = 0;
                sXY_int Tile = MouseOverTerrian.Tile.Normal;

                if ( Map.Selected_Tile_A != null )
                {
                    if ( Tile.X == Map.Selected_Tile_A.X )
                    {
                        if ( Tile.Y <= Map.Selected_Tile_A.Y )
                        {
                            A = Tile.Y;
                            B = Map.Selected_Tile_A.Y;
                        }
                        else
                        {
                            A = Map.Selected_Tile_A.Y;
                            B = Tile.Y;
                        }
                        clsUnitCreate objectCreator = new clsUnitCreate();
                        Map.SetObjectCreatorDefaults(objectCreator);
                        for ( Num = A; Num <= B; Num++ )
                        {
                            objectCreator.Horizontal.X = (int)((Tile.X + 0.5D) * App.TerrainGridSpacing);
                            objectCreator.Horizontal.Y = (int)((Num + 0.5D) * App.TerrainGridSpacing);
                            objectCreator.Perform();
                        }

                        Map.UndoStepCreate("Object Line");
                        Map.Update();
                        Map.MinimapMakeLater();
                        Map.Selected_Tile_A = null;
                        MapViewControl.DrawViewLater();
                    }
                    else if ( Tile.Y == Map.Selected_Tile_A.Y )
                    {
                        if ( Tile.X <= Map.Selected_Tile_A.X )
                        {
                            A = Tile.X;
                            B = Map.Selected_Tile_A.X;
                        }
                        else
                        {
                            A = Map.Selected_Tile_A.X;
                            B = Tile.X;
                        }
                        clsUnitCreate objectCreator = new clsUnitCreate();
                        Map.SetObjectCreatorDefaults(objectCreator);
                        for ( Num = A; Num <= B; Num++ )
                        {
                            objectCreator.Horizontal.X = (int)((Num + 0.5D) * App.TerrainGridSpacing);
                            objectCreator.Horizontal.Y = (int)((Tile.Y + 0.5D) * App.TerrainGridSpacing);
                            objectCreator.Perform();
                        }

                        Map.UndoStepCreate("Object Line");
                        Map.Update();
                        Map.MinimapMakeLater();
                        Map.Selected_Tile_A = null;
                        MapViewControl.DrawViewLater();
                    }
                    else
                    {
                    }
                }
                else
                {
                    Map.Selected_Tile_A = new clsXY_int(Tile);
                }
            }
        }
    }
}