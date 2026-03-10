/**
 * Converts bytes to a human-readable string (e.g., 1 KB, 2.5 MB)
 * @param bytes The number of bytes to convert
 * @param decimals How many decimal places to include (default 2)
 * 
 * thanks Gemini AI
 */
export const formatFileSize = (bytes: number, decimals: number = 2): string => {
  if (bytes === 0) return '0 Bytes';

  const k = 1024;
  const dm = decimals < 0 ? 0 : decimals;
  const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB'];

  // Calculate which unit index to use
  const i = Math.floor(Math.log(bytes) / Math.log(k));

  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(dm))} ${sizes[i]}`;
};


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
  const ext = extIndex !== -1 ? value.substring(extIndex) : ""
  if (name.length <= maxLength) return value

  maxLength = maxLength - ext.length
  const start = name.substring(0, Math.floor(maxLength / 2))
  const end = name.substring(name.length - Math.floor(maxLength / 2))

  return `${start}...${end}${ext}`
}