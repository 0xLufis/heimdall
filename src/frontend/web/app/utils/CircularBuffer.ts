/**
 * Zero-allocation high-frequency Circular Buffer backed by Float32Array.
 * Optimized for 60+ Hz industrial telemetry signal streaming and Canvas 2D / WebGL rendering.
 */
export class CircularBuffer {
  private buffer: Float32Array
  private head: number = 0
  private count: number = 0
  private writes: number = 0
  readonly capacity: number

  constructor(capacity: number = 120) {
    if (capacity <= 0) {
      throw new Error('Capacity must be greater than 0')
    }
    this.capacity = capacity
    this.buffer = new Float32Array(capacity)
  }

  /**
   * Push a new sample into the buffer. O(1), zero allocation.
   */
  push(value: number): void {
    this.buffer[this.head] = value
    this.head = (this.head + 1) % this.capacity
    this.writes++
    if (this.count < this.capacity) {
      this.count++
    }
  }

  /**
   * Monotonic change counter (pushes and clears). Lets renderers skip frames when nothing changed.
   */
  get version(): number {
    return this.writes
  }

  /**
   * Current number of populated items.
   */
  get length(): number {
    return this.count
  }

  /**
   * Get the most recently pushed value.
   */
  last(): number {
    if (this.count === 0) return 0
    const lastIndex = (this.head - 1 + this.capacity) % this.capacity
    return this.buffer[lastIndex]
  }

  /**
   * Returns elements ordered chronologically from oldest to newest.
   */
  toArray(): Float32Array {
    const out = new Float32Array(this.count)
    const start = (this.head - this.count + this.capacity) % this.capacity
    for (let i = 0; i < this.count; i++) {
      out[i] = this.buffer[(start + i) % this.capacity]
    }
    return out
  }

  /**
   * Minimum value currently in the buffer.
   */
  min(): number {
    if (this.count === 0) return 0
    let m = Infinity
    for (let i = 0; i < this.count; i++) {
      const v = this.buffer[i]
      if (v < m) m = v
    }
    return m === Infinity ? 0 : m
  }

  /**
   * Maximum value currently in the buffer.
   */
  max(): number {
    if (this.count === 0) return 0
    let m = -Infinity
    for (let i = 0; i < this.count; i++) {
      const v = this.buffer[i]
      if (v > m) m = v
    }
    return m === -Infinity ? 0 : m
  }

  /**
   * Average of values currently in the buffer.
   */
  avg(): number {
    if (this.count === 0) return 0
    let sum = 0
    for (let i = 0; i < this.count; i++) {
      sum += this.buffer[i]
    }
    return sum / this.count
  }

  /**
   * Reset the buffer count and pointer.
   */
  clear(): void {
    this.head = 0
    this.count = 0
    this.writes++
    this.buffer.fill(0)
  }
}
