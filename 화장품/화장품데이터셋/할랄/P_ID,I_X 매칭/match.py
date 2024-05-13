import pandas as pd

# Load the provided Excel files
product_df = pd.read_excel("product.xlsx")
e_ing_df = pd.read_excel("E_ingredients_final.xlsx")

# Splitting the ingredients in the 'Ingredient' column into separate items
# and matching them with the 'KName' in e_ing.xlsx
results = []

for _, row in product_df.iterrows():
    p_id = row['P_ID']
    ingredients = row['Ingredient'].split(', ')

    for ingredient in ingredients:
        # Matching ingredient with KName
        matching_rows = e_ing_df[e_ing_df['KName'] == ingredient]
        if not matching_rows.empty:
            for _, match_row in matching_rows.iterrows():
                results.append([p_id, match_row['ID']])
        else:
            results.append([p_id, '#'])

# Creating a new DataFrame for the results
result_df = pd.DataFrame(results, columns=['P_ID', 'ID'])

# Saving the result as a new Excel file
result_file_path = "path_to_save_matched_ingredients.xlsx"
result_df.to_excel(result_file_path, index=False)
