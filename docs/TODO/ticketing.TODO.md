### Ticketing flow

- Heimdall-agent || Mobile App || Web app >> HTTPS message /api/v1/tickets. -- Eval on the back-end show on the frontend >> Do stuff >> OnUpdate notify user (escalation or state change)
- Tickets should track if they are transient issues, maintanance, improvements or other.
- There should a flag for line stoppage
- There should be configurable 'responsible department' (Assy, smt, test, IT, MES, SAP, product owner ETC.) which lets users sort issues only linked to say precess engineering, as to evaluate all departments how much stoppage time they are responsible for
- There should an escalation system, where either manually or by some configurable trigger be escaleted to by default the dedicated engineer or to management. These triggers can be automatic (say robot collisions trigger automatic escalations) or by a configurable time spent in line stop.
- These tickets should trigger snapshot of the telemtry data-points at ticket raise.
- Tickets should accept comments, short descriptions, long descriptions and attachments, image attachments should have a rendered thumbnail.
- Tickets should have logging of changes, but should allowed to be edited going back in time, to add context or fix typos
- Escalations should have a handover state. Handing the ticket off should a sub-state to differntiate escalations where notification, hand off of work, paralell work should be allowed states
- the pedning state should have a configurable pending {string} by default, parts, sign-off, external or custom.

### Ticketing Meta Data

- Store the controllers ID as to be able to trace back to it
- Store time stamp and user data when applicable
  -- This can be either the heimdall user or an externally provided user from the system (say tracking the operator at hand)
- Automatically raised by machines, scheduled preventive maintanance and manually raised should be differntiated
- There should be a tagging option where tags can be added to group tickets.

### UI UX

- The frontend should have a personal header where tickets that are dedicated to the user (via the dedication interface) if empty hide
- There should be a monitor view for open tickets, weekly stoppage time stats, top worst machines
  -- This view should be allowd to either or use a list view with color coded stripes OR a kanban board. The list views default sorting should follow the ticket lifecycle and in general tickets that are equal in primary sorting should be secondarily sorted by time opened
- There should be a QR code system for quickly picking up tickets, there should a config to allow remote start or QR only start.
- Reaction time should be calculated from QR start when enabled
- Technicians should be able to reserve tickets when open.
- The list view should have configurable colums, must haves are: Ticket ID, Machines Name + Machine user identifier, Heimdall-agent host computer name, Prod cell/line name, Raised at, started at, originitator (user or automatic), currently working on it, there should a escalation badge too.
