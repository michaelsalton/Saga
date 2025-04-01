using System;
using UnityEngine;

namespace _Saga.Code.DataType
{
    public struct TimeData
    {
        public int DayOfYear;
        public int Year;
        public int Month;
        public int Day;
        public int Hour;
        public int Minute;

        public TimeData(int year, int month, int day, int hour, int minute)
        {
            Year = year;
            Month = month;
            Day = day;
            Hour = hour;
            Minute = minute;
            DayOfYear = CalculateDayOfYear(year, month, day);
        }
        
        public static int CalculateDayOfYear(int year, int month, int day)
        {
            var date = new DateTime(year, month, day);
            return date.DayOfYear;
        }
    }
}