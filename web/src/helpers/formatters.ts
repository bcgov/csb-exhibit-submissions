/**
 * Converts bytes to a human-readable string (e.g., 1 KB, 2.5 MB)
 * @param bytes The number of bytes to convert
 * @param decimals How many decimal places to include (default 2)
 *
 * thanks Gemini AI
 */
export const formatFileSize = (bytes: number, decimals: number = 2): string => {
  if (bytes === 0) return '0 Bytes'

  const k = 1024
  const dm = decimals < 0 ? 0 : decimals
  const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB']

  // Calculate which unit index to use
  const i = Math.floor(Math.log(bytes) / Math.log(k))

  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(dm))} ${sizes[i]}`
}

/**
 * Shorten a string so it fits within a specified max length
 * @param filename The value to shorten
 * @param decimals How many decimal places to include (default 2)
 *
 * thanks ChatGPT
 */
export const shortenString = (value: string, maxLength = 30): string => {
  const extIndex = value.lastIndexOf('.')
  //   if (extIndex === -1) return filename
  const name = extIndex === -1 ? value : value.substring(0, extIndex)
  const ext = extIndex !== -1 ? value.substring(extIndex) : ''
  if (name.length <= maxLength) return value

  maxLength = maxLength - ext.length
  const start = name.substring(0, Math.floor(maxLength / 2))
  const end = name.substring(name.length - Math.floor(maxLength / 2))

  return `${start}...${end}${ext}`
}

/**
 * Extracts a 24-hour time string from an ISO date string.
 * @param isoString - The ISO date string (e.g., "2026-03-30T09:30:00")
 * @returns A 24-hour formatted string (e.g., "09:30")
 */
export const formatStringTo24hrTime = (isoString: string): string => {
  if (!isoString) return ''

  const date = new Date(isoString)

  if (isNaN(date.getTime())) {
    return ''
  }

  return new Intl.DateTimeFormat('en-GB', {
    // 'en-GB' defaults to 24h
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(date)
}

/**
 * Extracts a 24-hour time string from a Date object or string.
 * @param date - The date object
 * @returns A 24-hour formatted string (e.g., "09:30")
 */
export const formatDateTo24hrTime = (dateInput: Date | string): string => {
  if (!dateInput) return ''

  // Ensure we are working with a Date object
  const date = typeof dateInput === 'string' ? new Date(dateInput) : dateInput

  // Check if the Date object is valid (e.g., not "Invalid Date")
  if (!(date instanceof Date) || isNaN(date.getTime())) {
    return ''
  }

  return new Intl.DateTimeFormat('en-CA', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(date)
}

export const formatDate = (dateInput: string | Date): string => {
  const date = typeof dateInput === 'string' ? new Date(dateInput) : dateInput
  // console.log(dateInput, typeof dateInput, date);

  return date.toLocaleDateString('en-CA', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  })
}

export const formatDateyyyymmdd = (dateInput: string | Date): string => {
  if (typeof dateInput === 'string' && dateInput == '') dateInput = new Date()
  const date = typeof dateInput === 'string' ? new Date(dateInput) : dateInput
  // console.log(dateInput, typeof dateInput, date);

  return date.toLocaleDateString('en-CA', {
    year: 'numeric',
    month: 'numeric',
    day: 'numeric',
  })
}

export const formatDateTime = (
  dateInput: string | Date | null,
  twentyFourHourFormat: boolean = false,
): string => {
  if (!dateInput) return ''
  const date = typeof dateInput === 'string' ? new Date(dateInput) : dateInput
  // console.log(dateInput, typeof dateInput, date);

  if (twentyFourHourFormat)
    return new Intl.DateTimeFormat('en-CA', {
      year: 'numeric',
      month: 'numeric',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    }).format(date)
  else
    return date.toLocaleString('en-CA', {
      year: 'numeric',
      month: 'numeric',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    })
}

export const localDateToUtc = (date: string, endOfDay: boolean = false): string => {
  if (!date) return ''

  const [year, month, day] = date.split('-').map(Number)

  if (!year || !month || !day) return ''

  const localDate = endOfDay
    ? new Date(year, month - 1, day, 23, 59, 59, 999)
    : new Date(year, month - 1, day, 0, 0, 0, 0)

  return localDate.toISOString()
}

export const convertLocalToUtc = (dateInput: string | Date): string => {
  const date = typeof dateInput === 'string' ? new Date(dateInput) : dateInput

  if (isNaN(date.getTime())) return ''

  return date.toISOString()
}

export const convertUtcToLocal = (utcString: string): Date | null => {
  if (!utcString) return null

  // Append 'Z' if it's missing to force JS to treat it as UTC
  const normalizedUtc = utcString.endsWith('Z') ? utcString : `${utcString}Z`
  const date = new Date(normalizedUtc)

  return isNaN(date.getTime()) ? null : date
}

export const splitDateTimeForDisplay = (dateTimeStr: string): { date: string; time: string } => {
  if (!dateTimeStr) {
    return { date: '', time: '' }
  }

  const parts = dateTimeStr.split(' ')

  if (parts.length !== 2) {
    return { date: dateTimeStr, time: '' }
  }

  const datePart = parts[0] // "2026-03-31"
  let timePart = parts[1] // "09:30:00.0"
  if (!timePart) {
    return { date: dateTimeStr, time: '' }
  }

  if (timePart.includes('.')) {
    timePart = timePart.split('.')[0] // Result: "09:30:00"
  }

  // 2. Strip the seconds so it just shows HH:mm
  const timeSegments = timePart!.split(':')
  if (timeSegments.length >= 2) {
    timePart = `${timeSegments[0]}:${timeSegments[1]}` // Result: "09:30"
  }

  return {
    date: datePart!,
    time: timePart!,
  }
}
