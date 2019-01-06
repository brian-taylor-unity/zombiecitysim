using System.Collections.Generic;
using UnityEngine;

public class CityGrid
{
    private int xSize;
    private int ySize;
    private Tile[,] grid;

    public CityGrid(int xSize, int ySize)
    {
        this.xSize = xSize;
        this.ySize = ySize;
        grid = new Tile[this.ySize, this.xSize];
    }

    public void AddTile(Tile tile, int x, int y)
    {
        if (x >= 0 && x < xSize &&
            y >= 0 && y < ySize)
        {
            grid[y, x] = tile;
        }
    }

    public bool TileExists(int x, int y)
    {
        return x >= 0 && x < xSize &&
               y >= 0 && y < ySize &&
               grid[y, x] != null;
    }

    public bool IsPassable(int x, int y)
    {
        return x >= 0 && x < xSize &&
               y >= 0 && y < ySize && 
               grid[y, x].Passable;
    }
}
