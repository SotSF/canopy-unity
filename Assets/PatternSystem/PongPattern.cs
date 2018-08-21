using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace sotsf.canopy.patterns
{
    public class PongPattern : Pattern
    {
        // Degrees per second; circumnavigate canopy in 2 seconds
        public float paddleSpeed = 180;
        //Transit entire canopy in 3 seconds
        public float ballSpeed = 150 / 3f;

        //Canopy polar space:
        // Radius: [0, 75]
        // Angle: [0, 360]

        // paddeLocation is the angular location (radians) of the paddle
        private float paddleLocation = 0;

        // First 2 are ball position
        // 0: radius
        // 1: angle
        // Second 2 are ball velocity
        // 2: speed
        // 3: heading
        private Vector4 ballData = new Vector4(35, 0, 150 / 3f, Mathf.PI / 2.59f);

        void CalculateBallPosition()
        {
            // If ball not at edge, move forward
            // if ball at edge and paddle at that position, reflect off the paddle
            // if ball at edge and paddle not at that position, score

            //Ball is not at edge
            var radius = ballData[0];
            var theta = ballData[1];
            var speed = ballData[2] * Time.deltaTime;
            var heading = ballData[3];

            if (radius <= 75)
            {
                Vector2 cartesianOld = new Vector2(radius * Mathf.Cos(theta), radius * Mathf.Sin(theta));
                Vector2 cartesianNew = new Vector2(speed * Mathf.Cos(heading), speed * Mathf.Sin(heading));
                Vector2 newPoint = cartesianOld + cartesianNew;
                ballData[0] = newPoint.magnitude;
                ballData[1] = Mathf.Atan2(newPoint.y, newPoint.x);
                if (ballData[1] < 0)
                    ballData[1] = Mathf.PI * 2 + ballData[1];
            }
            //Ball is at edge
            else
            {
                ballData[0] = 75;
                ballData[1] += Mathf.PI;
                ballData[1] %= 2 * Mathf.PI;
            }
        }

        protected void UpdateRenderParams()
        {
            // Read input from XboxController.instance
            // update paddle state, ball state
            // set values on shader
            float xInput = 0;
            if (XboxController.instance != null)
            {
                xInput = XboxController.instance.Get(XboxController.ControlInput.leftStickX);
            }

            paddleLocation += (xInput * paddleSpeed) % 360;
            CalculateBallPosition();
            renderParams["paddleLocation"] = paddleLocation;
            patternShader.SetVector("ballData", ballData);
        }
    }
}