namespace SpaceshipGame
{
    public enum SpaceshipGameEventType
    {
        Update = 1,
        ChangeColor,
        Press,
        Gyro,
        Rotate,
        CalibrationStatus,
        ShipPosition,
        TouchPosition,
        GameDataUpdate
    }
    public enum PlayerType
    {
        Web,
        Controller,
        Oddball,
        GenericCanvas
    }
    public enum PlayerState
    {
        Alive,
        Dead,
        Spawning,
        Idle
    }

}