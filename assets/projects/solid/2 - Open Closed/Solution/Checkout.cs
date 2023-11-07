using System;

namespace OCP.Solution
{
    public class Checkout : CheckService
    {
        public override void RealizarCheck(Check check)
        {
            //Verify if it hasn't a checkin without checkout and other business logics

            //Verify if hasn't conflicted with the previous checkin timestamp

            //Populate checkout data into visit

            //save
        }
    }
}
