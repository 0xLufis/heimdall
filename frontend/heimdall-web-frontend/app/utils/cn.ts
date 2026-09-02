import { type ClassValue, clsx } from 'clsx'
import { twMerge } from 'tailwind-merge'

/**
 * Conditionally joins Tailwind CSS classes and merges them.
 * Utilizes `clsx` for conditional class application and `tailwind-merge`
 * for intelligently merging Tailwind classes without conflicts.
 *
 * @param {...ClassValue} inputs - A list of class values (strings, objects, arrays) to be joined and merged.
 * @returns {string} The merged Tailwind CSS class string.
 */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}
