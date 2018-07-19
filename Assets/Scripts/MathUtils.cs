using UnityEngine;
using System.Collections;
using System;

public static class MathUtils
{

    /**
     * given two points a = [ax, ay] and b = [bx, by] in the vertical plane,
     * rope length rLength, and the number of intermediate points N,
     * outputs the coordinates X and Y of the hanging rope from a to b
     * the optional input sagInit initializes the sag parameter for the
     * root-finding procedure.
     *
     * Ported from MATLAB code written by Yuval:
     * https://www.mathworks.com/matlabcentral/fileexchange/38550-catenary-hanging-rope-between-two-points
     */
    public static Vector2[] Catenary(Vector2 a, Vector2 b, float rLength, int N, float sagInit)
    {
        int maxIter = 100;     // maximum number of iterations
        float minGrad = 1e-10f;   // minimum norm of gradient
        float minVal = 1e-8f;    // minimum norm of sag function
        float stepDec = 0.5f;     // factor for decreasing stepsize
        float minStep = 1e-9f;    // minimum step size
        float minHoriz = 1e-3f;    // minumum horizontal distance
        float sag = sagInit;
        float[] X = new float[N];
        float[] Y = new float[N];
        int i;

        if (a[0] > b[0])
        {
            Vector2 tmp = b;
            b = a;
            a = tmp;
        }

        float d = b[0] - a[0];
        float h = b[1] - a[1];

        if (Mathf.Abs(d) < minHoriz)
        {
            // almost perfectly vertical
            for (i = 0; i < N; i++)
            {
                X[i] = (a[0] + b[0]) / 2;
            }

            if (rLength < Mathf.Abs(h))
            {
                // rope is stretched
                Y = LinearVector(a[1], b[1], N);
            }
            else
            {
                sag = (rLength - Mathf.Abs(h)) / 2;
                int nSag = (int)Mathf.Ceil(N * sag / rLength);
                float yMax = Mathf.Max(a[1], b[1]);
                float yMin = Mathf.Min(a[1], b[1]);
                var first = LinearVector(yMax, yMin - sag, N - nSag);
                var second = LinearVector(yMin - sag, yMin, nSag);
                Y = new float[first.Length + second.Length];
                first.CopyTo(Y, 0);
                second.CopyTo(Y, first.Length);
                  
            }

            return Zip(X, Y);
        }

        X = LinearVector(a[0], b[0], N);

        if (rLength <= Mathf.Sqrt(Mathf.Pow(d, 2) + Mathf.Pow(h, 2)))
        {
            // rope is stretched: straight line
            Y = LinearVector(a[1], b[1], N);
        }
        else
        {
            // find rope sag
            for (int iter = 0; iter < maxIter; iter++)
            {
                float val = g(sag, d, h, rLength);
                float grad = dg(sag, d);

                if (Mathf.Abs(val) < minVal || Mathf.Abs(grad) < minGrad)
                {
                    break;
                }

                float search = -g(sag, d, h, rLength) / dg(sag, d);
                float alpha = 1;
                float sagNew = sag + alpha * search;

                while (sagNew < 0 || Mathf.Abs(g(sagNew, d, h, rLength)) > Mathf.Abs(val))
                {
                    alpha = stepDec * alpha;
                    if (alpha < minStep)
                    {
                        break;
                    }

                    sagNew = sag + alpha * search;
                }

                sag = sagNew;
            }

            // get location of rope minimum and vertical bias
            float xLeft = 0.5f * (Mathf.Log((rLength + h) / (rLength - h)) / sag - d);
            float xMin = a[0] - xLeft;
            float bias = (float)(a[1] - Math.Cosh(xLeft * sag) / sag);

            for (i = 0; i < Y.Length; i++)
            {
                Y[i] = (float)(Math.Cosh((X[i] - xMin) * sag) / sag + bias);
            }
        }

        return Zip(X, Y);
    }

    public static Vector2[] Catenary(Vector2 a, Vector2 b, float rLength, int N)
    {
        return Catenary(a, b, rLength, N, 1);
    }


    /********************************************************************************
     * Helper methods
     ********************************************************************************/

    /**
     * Mocks the MATLAB `linspace` method:
     *   https://www.mathworks.com/help/matlab/ref/linspace.html#bufmmx4
     *
     * Generates a linearly spaced vector of `n` points in the interval[`x1`, `x2`] 
     */
    static float[] LinearVector(float x1, float x2, int n)
    {
        //float[] result = new float[n];
        //for (int i = 0; i < n; i++) {
        //    result[i] = Mathf.Lerp(x1, x2, i / (n - 1));
        //}
        //return result;

        float[] vector = new float[n];
        vector[0] = x1;
        vector[n - 1] = x2;

        float spacingInterval = (x2 - x1) / (n - 1);
        for (int i = 1; i < n; i++)
        {
            vector[i] = x1 + spacingInterval * i;
        }

        return vector;
    }

    /**
     * Converts two arrays of floats to one array of Vector2s
     */
    static Vector2[] Zip(float[] X, float[] Y)
    {
        Vector2[] coords = new Vector2[X.Length];
        for (int i = 0; i < X.Length; i++)
        {
            coords[i] = new Vector2(X[i], Y[i]);
        }
        return coords;
    }

    /**
     * Not exactly sure what these two methods do...
     */
    static float g(float s, float d, float h, float rLength)
    {
        return (float)(2 * Math.Sinh(s * d / 2) / s - Mathf.Sqrt(Mathf.Pow(rLength, 2) - Mathf.Pow(h, 2)));
    }

    static float dg(float s, float d)
    {
        return (float)(2 * Math.Cosh(s * d / 2) * d / (2 * s) - (2 * Math.Sinh(s * d / 2) / Mathf.Pow(s, 2)));
    }
}
