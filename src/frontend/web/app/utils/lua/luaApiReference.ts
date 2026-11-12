/**
 * Functions the Lua sandbox exposes, one entry per `table["name"]` registered in
 * src/shared/lua/Apis/*.cs. A unit test parses those files and fails when this list and the
 * backend drift apart, so the inline reference on the scripts page stays truthful.
 */

export interface LuaApiFunction {
  name: string
  signature: string
  description: string
  /** Changes plant data (tickets, machines, groupings, events); a test run executes it for real. */
  mutates?: boolean
}

export interface LuaApiGroup {
  global: string
  description: string
  functions: LuaApiFunction[]
}

export const LUA_API_REFERENCE: LuaApiGroup[] = [
  {
    global: 'machines',
    description: 'Stations: id, name, display_name, custom_identifier, machine_type, status, operational_state, criticality, group_id, technologies …',
    functions: [
      { name: 'get', signature: 'machines.get(id)', description: 'One station as a table, nil when unknown.' },
      { name: 'list', signature: "machines.list({ status = 'active' })", description: 'Stations, optionally filtered.' },
      { name: 'is_operational', signature: 'machines.is_operational(id)', description: 'true when the station runs.' },
      { name: 'get_property', signature: 'machines.get_property(id, key)', description: 'A custom property value.' },
      { name: 'set_property', signature: 'machines.set_property(id, key, value)', description: 'Stores a custom property.', mutates: true },
      { name: 'update_status', signature: 'machines.update_status(id, status, reason)', description: 'Changes the station status.', mutates: true }
    ]
  },
  {
    global: 'telemetry',
    description: 'Station signals by metric name (e.g. parts_produced, scrap_parts, cycle_time_s, target_cycle_time_s, availability_pct).',
    functions: [
      { name: 'get_latest', signature: 'telemetry.get_latest(machine_id, metric)', description: 'Latest value (number) or nil.' },
      { name: 'get_history', signature: 'telemetry.get_history(machine_id, metric, count)', description: 'Newest points as { value, timestamp } tables.' },
      { name: 'moving_average', signature: 'telemetry.moving_average(history, window)', description: 'Average of the last `window` points.' },
      { name: 'detect_spike', signature: 'telemetry.detect_spike(value, history, sigma)', description: '{ is_spike, … } against the history.' },
      { name: 'process_change', signature: 'telemetry.process_change(machine_id, metric, old, new)', description: 'Runs the registered on_change handlers.' },
      { name: 'on_change', signature: 'telemetry.on_change(metric, function(evt) … end)', description: 'Handler for telemetry change triggers.' },
      { name: 'emit', signature: 'telemetry.emit(machine_id, metric, value, tags)', description: 'Publishes a derived data point.', mutates: true }
    ]
  },
  {
    global: 'tickets',
    description: 'Maintenance tickets: id, title, status, priority, issue_type, is_line_stop, machine_id, created_at, sla_hours …',
    functions: [
      { name: 'get', signature: 'tickets.get(id)', description: 'One ticket or nil.' },
      { name: 'list', signature: "tickets.list({ machine_id = id, status = 'Open' })", description: 'Tickets filtered by status, machine_id, priority, is_escalated.' },
      { name: 'check_sla', signature: 'tickets.check_sla(id, max_hours)', description: '{ is_breached, elapsed_minutes, remaining_minutes }.' },
      { name: 'create', signature: "tickets.create({ title = '…', machine_id = id, priority = 'High' })", description: 'Opens a ticket.', mutates: true },
      { name: 'update_status', signature: 'tickets.update_status(id, status, notes)', description: 'Changes the ticket status.', mutates: true },
      { name: 'escalate', signature: 'tickets.escalate(id, reason, by, target)', description: 'Escalates a ticket.', mutates: true },
      { name: 'trigger_preventive_maintenance', signature: 'tickets.trigger_preventive_maintenance(machine_id, reason, opts)', description: 'Opens a preventive maintenance ticket.', mutates: true },
      { name: 'create_from_analytics', signature: 'tickets.create_from_analytics({ machine_id, anomaly_type, … })', description: 'Opens an analytics ticket.', mutates: true }
    ]
  },
  {
    global: 'groupings',
    description: "Machine groups (group_type 'machine').",
    functions: [
      { name: 'get', signature: "groupings.get(id, 'machine')", description: 'One group.' },
      { name: 'list', signature: "groupings.list('machine')", description: 'All groups.' },
      { name: 'get_members', signature: "groupings.get_members(group_id, 'machine')", description: 'Member stations.' },
      { name: 'is_member', signature: "groupings.is_member(group_id, machine_id, 'machine')", description: 'Membership check.' },
      { name: 'define_rule', signature: "groupings.define_rule(name, 'machine', function(m) … end)", description: 'Named membership predicate.' },
      { name: 'evaluate_rule', signature: 'groupings.evaluate_rule(name)', description: 'Stations matching a rule.' },
      { name: 'add_member', signature: "groupings.add_member(group_id, machine_id, 'machine')", description: 'Adds a station.', mutates: true },
      { name: 'remove_member', signature: "groupings.remove_member(group_id, machine_id, 'machine')", description: 'Removes a station.', mutates: true }
    ]
  },
  {
    global: 'events',
    description: 'Script-level events.',
    functions: [
      { name: 'on', signature: 'events.on(name, function(payload) … end)', description: 'Subscribes a handler.' },
      { name: 'trigger', signature: 'events.trigger(name, payload)', description: 'Runs the handlers of an event.', mutates: true }
    ]
  },
  {
    global: 'ui',
    description: 'Dynamic UI extensions (scripts with the UI Extension trigger).',
    functions: [
      { name: 'register_action', signature: "ui.register_action({ id, title, target = 'machine', handler = function(ctx) … end })", description: 'Adds an action button.' },
      { name: 'register_badge', signature: "ui.register_badge({ id, title, target = 'ticket', color })", description: 'Adds a badge.' },
      { name: 'register_banner', signature: 'ui.register_banner({ id, title, … })', description: 'Adds a banner.' },
      { name: 'get_extensions', signature: "ui.get_extensions('machine')", description: 'Registered extensions of a target.' },
      { name: 'invoke_action', signature: 'ui.invoke_action(id, ctx)', description: 'Runs an action handler.' }
    ]
  }
]

/** Globals a script receives from the run context (test run station picker, telemetry triggers). */
export const LUA_CONTEXT_GLOBALS: Array<{ name: string; description: string }> = [
  { name: 'context_machine_id', description: 'Station id of a test run or telemetry trigger.' },
  { name: 'context_metric', description: 'Metric that changed (telemetry trigger).' },
  { name: 'context_value', description: 'New value (telemetry trigger).' },
  { name: 'context_test_mode', description: 'true during a test run.' }
]
