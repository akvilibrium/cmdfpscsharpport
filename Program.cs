using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.InteropServices;

namespace testcmdfps {
    class Program {
        static void Main(string[] args) {
            Game game = new Game();
            game.main();
        }
    }

    class Game {
        int nScreenWidth = 120;         // Console Screen Size X (columns)
        int nScreenHeight = 40;         // Console Screen Size Y (rows)
        int nMapWidth = 16;             // World Dimensions
        int nMapHeight = 16;

        double fPlayerX = 14.7f;         // Player Start Position
        double fPlayerY = 5.09f;
        double fPlayerA = 0.0f;          // Player Start Rotation
        double fFOV = 3.14159f / 4.0f;   // Field of View
        double fDepth = 16.0f;           // Maximum rendering distance
        double fSpeed = 0.0000005; //5.0f;			// Walking Speed

        public int main() {

            // Create Screen Buffer
            char[] screen = new char[nScreenWidth * nScreenHeight];
            IntPtr hConsole = CTS.CreateConsoleScreenBuffer(CTS.GENERIC_READ | CTS.GENERIC_WRITE, 0, IntPtr.Zero, 1, IntPtr.Zero);
            CTS.SetConsoleActiveScreenBuffer(hConsole);
            uint dwBytesWritten = 0;

            // Create Map of world space # = wall block, . = space
            string map = "";
            map += "#########.......";
            map += "#...............";
            map += "#.......########";
            map += "#..............#";
            map += "#......##......#";
            map += "#......##......#";
            map += "#..............#";
            map += "###............#";
            map += "##.............#";
            map += "#......####..###";
            map += "#......#.......#";
            map += "#......#.......#";
            map += "#..............#";
            map += "#......#########";
            map += "#..............#";
            map += "################";

            var tp1 = DateTime.Now;
            var tp2 = DateTime.Now;

            while (true) {
                // We'll need time differential per frame to calculate modification
                // to movement speeds, to ensure consistant movement, as ray-tracing
                // is non-deterministic
                tp2 = DateTime.Now;
                TimeSpan elapsedTime = tp2 - tp1;
                tp1 = tp2;
                long fElapsedTime = elapsedTime.Ticks;

                // Handle CCW Rotation
                if (CTS.IsKeyPushedDown((int)'A'))
                    fPlayerA -= (fSpeed * 0.75f) * fElapsedTime;

                // Handle CW Rotation
                if (CTS.IsKeyPushedDown((int)'D'))
                    fPlayerA += (fSpeed * 0.75f) * fElapsedTime;

                // Handle Forwards movement & collision
                if (CTS.IsKeyPushedDown((int)'W')) {
                    fPlayerX += Math.Sin(fPlayerA) * fSpeed * fElapsedTime;
                    fPlayerY += Math.Cos(fPlayerA) * fSpeed * fElapsedTime;
                    if (map[(int)fPlayerX * nMapWidth + (int)fPlayerY] == '#') {
                        fPlayerX -= Math.Sin(fPlayerA) * fSpeed * fElapsedTime;
                        fPlayerY -= Math.Cos(fPlayerA) * fSpeed * fElapsedTime;
                    }
                }

                // Handle backwards movement & collision
                if (CTS.IsKeyPushedDown((int)'S')) {
                    fPlayerX -= Math.Sin(fPlayerA) * fSpeed * fElapsedTime; ;
                    fPlayerY -= Math.Cos(fPlayerA) * fSpeed * fElapsedTime; ;
                    if (map[(int)fPlayerX * nMapWidth + (int)fPlayerY] == '#') {
                        fPlayerX += Math.Sin(fPlayerA) * fSpeed * fElapsedTime; ;
                        fPlayerY += Math.Cos(fPlayerA) * fSpeed * fElapsedTime; ;
                    }
                }
                for (int x = 0; x < nScreenWidth; x++) {
                    // For each column, calculate the projected ray angle into world space
                    double fRayAngle = (fPlayerA - fFOV / 2.0f) + ((double)x / (double)nScreenWidth) * fFOV;

                    // Find distance to wall
                    double fStepSize = 0.1f;       // Increment size for ray casting, decrease to increase										
                    double fDistanceToWall = 0.0f; //                                      resolution

                    bool bHitWall = false;      // Set when ray hits wall block
                    bool bBoundary = false;     // Set when ray hits boundary between two wall blocks

                    double fEyeX = Math.Sin(fRayAngle); // Unit vector for ray in player space
                    double fEyeY = Math.Cos(fRayAngle);

                    // Incrementally cast ray from player, along ray angle, testing for 
                    // intersection with a block
                    while (!bHitWall && fDistanceToWall < fDepth) {
                        fDistanceToWall += fStepSize;
                        int nTestX = (int)(fPlayerX + fEyeX * fDistanceToWall);
                        int nTestY = (int)(fPlayerY + fEyeY * fDistanceToWall);

                        // Test if ray is out of bounds
                        if (nTestX < 0 || nTestX >= nMapWidth || nTestY < 0 || nTestY >= nMapHeight) {
                            bHitWall = true;            // Just set distance to maximum depth
                            fDistanceToWall = fDepth;
                        } else {
                            // Ray is inbounds so test to see if the ray cell is a wall block
                            if (map[nTestX * nMapWidth + nTestY] == '#') {
                                // Ray has hit wall
                                bHitWall = true;

                                // To highlight tile boundaries, cast a ray from each corner
                                // of the tile, to the player. The more coincident this ray
                                // is to the rendering ray, the closer we are to a tile 
                                // boundary, which we'll shade to add detail to the walls
                                List<Tuple<double, double>> p = new List<Tuple<double, double>>();

                                // Test each corner of hit tile, storing the distance from
                                // the player, and the calculated dot product of the two rays
                                for (int tx = 0; tx < 2; tx++)
                                    for (int ty = 0; ty < 2; ty++) {
                                        // Angle of corner to eye
                                        double vy = (float)nTestY + ty - fPlayerY;
                                        double vx = (float)nTestX + tx - fPlayerX;
                                        double d = Math.Sqrt(vx * vx + vy * vy);
                                        double dot = (fEyeX * vx / d) + (fEyeY * vy / d);
                                        p.Add(new Tuple<double, double>(d, dot));
                                    }

                                // Sort Pairs from closest to farthest
                                p.Sort((a, b) => a.Item1.CompareTo(b.Item1));


                                // First two/three are closest (we will never see all four)
                                double fBound = 0.01;
                                if (Math.Acos(p[0].Item2) < fBound) bBoundary = true;
                                if (Math.Acos(p[1].Item2) < fBound) bBoundary = true;
                                if (Math.Acos(p[2].Item2) < fBound) bBoundary = true;
                            }
                        }
                    }
                    // Calculate distance to ceiling and floor
                    int nCeiling = (int)((double)(nScreenHeight / 2.0) - nScreenHeight / ((double)fDistanceToWall));
                    int nFloor = nScreenHeight - nCeiling;

                    // Shader walls based on distance
                    char nShade = ' ';
                    if (fDistanceToWall <= fDepth / 4.0f) nShade = (char)0x2588;  // Very close	
                    else if (fDistanceToWall < fDepth / 3.0f) nShade = (char)0x2593;
                    else if (fDistanceToWall < fDepth / 2.0f) nShade = (char)0x2592;
                    else if (fDistanceToWall < fDepth) nShade = (char)0x2591;
                    else nShade = ' ';      // Too far away

                    if (bBoundary) nShade = ' '; // Black it out

                    for (int y = 0; y < nScreenHeight; y++) {
                        // Each Row
                        if (y <= nCeiling)
                            screen[y * nScreenWidth + x] = ' ';
                        else if (y > nCeiling && y <= nFloor)
                            screen[y * nScreenWidth + x] = nShade;
                        else // Floor
                        {
                            // Shade floor based on distance
                            double b = 1.0f - (((double)y - nScreenHeight / 2.0f) / ((double)nScreenHeight / 2.0f));
                            if (b < 0.25) nShade = '#';
                            else if (b < 0.5) nShade = 'x';
                            else if (b < 0.75) nShade = '.';
                            else if (b < 0.9) nShade = '-';
                            else nShade = ' ';
                            screen[y * nScreenWidth + x] = nShade;
                        }
                    }
                }
                // Display Stats
                //swprintf_s(screen, 40, L"X=%3.2f, Y=%3.2f, A=%3.2f FPS=%3.2f ", fPlayerX, fPlayerY, fPlayerA, 1.0f / fElapsedTime);

                // Display Map
                for (int nx = 0; nx < nMapWidth; nx++)
                    for (int ny = 0; ny < nMapWidth; ny++) {
                        screen[(ny + 1) * nScreenWidth + nx] = map[ny * nMapWidth + nx];
                    }
                screen[((int)fPlayerX + 1) * nScreenWidth + (int)fPlayerY] = 'P';

                // Display Frame
                screen[nScreenWidth * nScreenHeight - 1] = '\0';
                //WriteConsoleOutputCharacter(hConsole, screen, nScreenWidth * nScreenHeight, { 0,0 }, &dwBytesWritten);
                CTS.WriteConsoleOutputCharacter(hConsole, new string(screen), (uint)(nScreenWidth * nScreenHeight), new CTS.COORD(0, 0), out dwBytesWritten);
            }
            return 0;
        }
    }

    public static class CTS { // console tools
        public const int GENERIC_READ = unchecked((int)0x80000000);
        public const int GENERIC_WRITE = 0x40000000;

        [DllImport("Kernel32.dll")]
        public static extern IntPtr CreateConsoleScreenBuffer(
            int dwDesiredAccess, int dwShareMode,
            IntPtr secutiryAttributes,
            UInt32 flags,
            IntPtr screenBufferData);

        [DllImport("kernel32.dll")]
        public static extern IntPtr SetConsoleActiveScreenBuffer(IntPtr hConsoleOutput);

        [DllImport("kernel32.dll")]
        public static extern bool WriteConsole(
            IntPtr hConsoleOutput, string lpBuffer,
            uint nNumberOfCharsToWrite, out uint lpNumberOfCharsWritten,
            IntPtr lpReserved);

        //

        [StructLayout(LayoutKind.Sequential)]
        public struct COORD {
            public short X;
            public short Y;

            public COORD(short X, short Y) {
                this.X = X;
                this.Y = Y;
            }
        };

        [DllImport("kernel32.dll")]
        public static extern bool WriteConsoleOutputCharacter(
            IntPtr hConsoleOutput,
            string lpCharacter, uint nLength, COORD dwWriteCoord,
            out uint lpNumberOfCharsWritten);

        //

        [DllImport("User32.dll")]
        public static extern short GetAsyncKeyState(Int32 vKey);
        public static bool IsKeyPushedDown(int vKey) {
            return 0 != (GetAsyncKeyState((int)vKey) & 0x8000);
        }
    }
}
