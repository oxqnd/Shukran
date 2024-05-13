import pandas as pd

# Load the provided CSV and Excel files
e_ingredients_path = 'E_ingredients_final.xlsx'
ha_test_path = 'Haram.xlsx'

# Read the files
e_ingredients_df = pd.read_excel(e_ingredients_path)
ha_test_df = pd.read_excel(ha_test_path)

# Function to match Korean ingredients with their English counterparts and return a string without spaces after commas
def match_and_replace_ingredients_v4(korean_ingredients, e_ingredients_df):
    # Splitting the Korean ingredients string into a list
    ingredients_list = korean_ingredients.split(',')

    # Creating a dictionary for KName to Name mapping
    kname_to_name_map = dict(zip(e_ingredients_df['KName'].str.upper(), e_ingredients_df['Name']))

    # For each Korean ingredient, try to find a match in the dictionary
    english_ingredients = []
    for ingredient in ingredients_list:
        # Removing whitespace and converting to uppercase for matching
        ingredient = ingredient.strip().upper()

        # Find a match in the dictionary or use '#' if no match is found
        matched_ingredient = kname_to_name_map.get(ingredient, '#')
        english_ingredients.append(matched_ingredient)

    # Joining the matched ingredients into a string, separated by commas without spaces
    return ','.join(english_ingredients)

# Applying the function to the '전성분' column of the ha_test dataframe
ha_test_df['I_Eng'] = ha_test_df['전성분'].apply(lambda x: match_and_replace_ingredients_v4(x, e_ingredients_df))

# Save the updated ha_test dataframe to a new Excel file with the non-spaced string format ingredients
updated_ha_test_path_v4 = 'Haram_final.xlsx'
ha_test_df.to_excel(updated_ha_test_path_v4, index=False)
