namespace WordApp.Models;

public static class ReviewSchedule
{
    public static int DaysForScore(int score) =>
        (int)Math.Clamp(Math.Round(1.0 + (score - 1) * (14.0 / 9.0)), 1, 15);
}
