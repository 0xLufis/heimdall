As the complexity of the project has become much much higher, defining a rule system for each and every aspect is crazy.
So lua scripting it is.

Feature to expose to the Lua API:
Machines
Groupings(Machines, Users, telemtry and essentially all things that can be grouped)
The telemtry API: allow for custom logic and better handling of changes
Tickets, automatic tickets should have an API too. From analytics or just SLA preventive maintanance

The goal:
Provide a safe scripting platform for plant specific rules and features to easily mold the system to the management structure of the plant at handling
Speed up development with, faster protypin and better separation of user level logic from system logic
Allow users to create their own custom logic where simpler rule-sets don't work well without using the rest API

Notes:
UI should not be replaced by lua only extended
If a feature can be made dynamic it should be
