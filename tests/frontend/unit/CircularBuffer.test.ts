import { describe, it, expect } from 'vitest'
import { CircularBuffer } from '../../../src/frontend/web/app/utils/CircularBuffer'

describe('CircularBuffer Utility Suite', () => {
  it('initializes with capacity and length 0', () => {
    const cb = new CircularBuffer(10)
    expect(cb.capacity).toBe(10)
    expect(cb.length).toBe(0)
    expect(cb.last()).toBe(0)
    expect(cb.min()).toBe(0)
    expect(cb.max()).toBe(0)
    expect(cb.avg()).toBe(0)
  })

  it('pushes elements and retrieves ordered array', () => {
    const cb = new CircularBuffer(5)
    cb.push(10)
    cb.push(20)
    cb.push(30)
    expect(cb.length).toBe(3)
    expect(cb.last()).toBe(30)

    const arr = cb.toArray()
    expect(Array.from(arr)).toEqual([10, 20, 30])
  })

  it('correctly overwrites oldest values when capacity is exceeded', () => {
    const cb = new CircularBuffer(3)
    cb.push(1)
    cb.push(2)
    cb.push(3)
    cb.push(4) // overwrites 1
    cb.push(5) // overwrites 2

    expect(cb.length).toBe(3)
    expect(cb.last()).toBe(5)
    expect(Array.from(cb.toArray())).toEqual([3, 4, 5])
  })

  it('computes min, max, and avg accurately', () => {
    const cb = new CircularBuffer(5)
    cb.push(10)
    cb.push(50)
    cb.push(20)

    expect(cb.min()).toBe(10)
    expect(cb.max()).toBe(50)
    expect(cb.avg()).toBeCloseTo(26.666, 2)
  })

  it('resets completely on clear()', () => {
    const cb = new CircularBuffer(5)
    cb.push(10)
    cb.push(20)
    cb.clear()

    expect(cb.length).toBe(0)
    expect(cb.last()).toBe(0)
    expect(Array.from(cb.toArray())).toEqual([])
  })
})
